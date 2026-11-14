package com.example.pickup_hyundai.data.auth

import android.content.Context
import com.example.pickup_hyundai.core.net.TokenSource
import com.example.pickup_hyundai.data.remote.AuthApi
import com.example.pickup_hyundai.data.remote.TokenResponse
import com.example.pickup_hyundai.data.remote.TokenResult
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock
import java.util.UUID

/**
 * 차량 로그인 세션.
 * - device_id: 최초 실행 시 만든 UUID를 계속 사용(QR 발급·토큰·OTP·계정 목록 공통)
 * - refresh token: 계정(session_id = loginSessionSeq)별로 Android Keystore 키로 암호화해 저장, 갱신 때마다 새 값으로 교체
 * - access token: 메모리에만 보관(1시간). 앱을 다시 켜면 현재 계정의 refresh token으로 새로 받는다
 * 토큰 값은 로그·화면에 남기지 않는다.
 */
class AuthSession(context: Context, private val api: AuthApi, private val devAccessToken: String = "") : TokenSource {

    private val prefs = context.applicationContext.getSharedPreferences("wayme_auth", Context.MODE_PRIVATE)
    private val mutex = Mutex()
    @Volatile private var access: String? = null
    @Volatile private var accessExpiresAtMs = 0L

    val deviceId: String = prefs.getString(K_DEVICE_ID, null) ?: UUID.randomUUID().toString().also {
        prefs.edit().putString(K_DEVICE_ID, it).apply()
    }

    /** 지금 이 차에서 쓰는 계정(session_id). null = 로그인 안 됨 */
    val currentSessionId: Long? get() = prefs.getLong(K_CURRENT, -1L).takeIf { it >= 0 }

    val isLoggedIn: Boolean get() = currentSessionId?.let { hasRefresh(it) } == true

    fun hasRefresh(sessionId: Long): Boolean = prefs.contains(refreshKey(sessionId))

    /** 토큰 응답 반영: 계정 refresh token 교체 + 현재 계정 전환 */
    fun accept(t: TokenResponse) {
        prefs.edit()
            .putString(refreshKey(t.sessionId), SecretBox.encrypt(t.refreshToken))
            .putLong(K_CURRENT, t.sessionId)
            .commit() // 새 refresh token을 즉시 기록(이전 값은 서버에서 더 이상 쓸 수 없음)
        access = t.accessToken
        accessExpiresAtMs = System.currentTimeMillis() + (t.expiresIn - 60).coerceAtLeast(60) * 1000L
    }

    /**
     * 계정 선택·access token 갱신(M-04). 실패(invalid_grant 등)하면 그 계정의 저장 토큰을 지운다.
     * 응답을 못 받은 경우(네트워크) 같은 토큰으로 재시도하면 서버가 같은 새 토큰을 준다(명세) → 저장값을 지우지 않는다.
     */
    suspend fun refresh(sessionId: Long): TokenResult = mutex.withLock {
        val rt = prefs.getString(refreshKey(sessionId), null)?.let(SecretBox::decrypt)
            ?: return@withLock TokenResult.OAuthError("invalid_grant")
        val r = api.tokenByRefresh(rt)
        when (r) {
            is TokenResult.Success -> accept(r.token)
            is TokenResult.OAuthError -> if (r.error == "invalid_grant" || r.error == "invalid_request") forget(sessionId)
        }
        r
    }

    /** 이 계정의 토큰·로컬 정보 삭제(로그아웃·만료) */
    fun forget(sessionId: Long) {
        val e = prefs.edit().remove(refreshKey(sessionId))
        if (currentSessionId == sessionId) {
            e.remove(K_CURRENT)
            access = null
            accessExpiresAtMs = 0
        }
        e.commit()
    }

    /** 현재 access token(만료 전) — 로그아웃 호출용 */
    fun currentAccessToken(): String? = access?.takeIf { System.currentTimeMillis() < accessExpiresAtMs }

    override suspend fun accessToken(): String? {
        currentAccessToken()?.let { return it }
        val sid = currentSessionId
        if (sid != null) {
            (refresh(sid) as? TokenResult.Success)?.let { return it.token.accessToken }
        }
        // 개발용(debug 빌드, local.properties): 로그인 전 차량 API 시험
        return devAccessToken.ifBlank { null }
    }

    override suspend fun refreshAfterUnauthorized(): String? {
        access = null
        val sid = currentSessionId ?: return null
        return (refresh(sid) as? TokenResult.Success)?.token?.accessToken
    }

    private fun refreshKey(sessionId: Long) = "rt_$sessionId"

    private companion object {
        const val K_DEVICE_ID = "device_id"
        const val K_CURRENT = "current_session"
    }
}
