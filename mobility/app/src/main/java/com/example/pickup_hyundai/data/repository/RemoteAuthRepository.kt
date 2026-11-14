package com.example.pickup_hyundai.data.repository

import com.example.pickup_hyundai.core.net.ApiException
import com.example.pickup_hyundai.data.auth.AuthSession
import com.example.pickup_hyundai.data.remote.AuthApi
import com.example.pickup_hyundai.data.remote.TokenResult
import com.example.pickup_hyundai.domain.model.LoginAccount
import com.example.pickup_hyundai.domain.model.LoginOutcome
import com.example.pickup_hyundai.domain.model.OtpRequest
import com.example.pickup_hyundai.domain.model.OtpVerify
import com.example.pickup_hyundai.domain.model.QrPoll
import com.example.pickup_hyundai.domain.model.QrSession

/**
 * 차량 인증 — WAY:ME 인증 API 명세서 M-01~M-08.
 * device_code는 이 클래스 안에만 보관(화면·로그·QR 노출 금지). refresh token은 AuthSession이 Keystore로 암호화 저장.
 */
class RemoteAuthRepository(
    private val api: AuthApi,
    private val session: AuthSession,
) : AuthRepository {

    override val isMock = false
    @Volatile private var deviceCode: String? = null

    override suspend fun getAccounts(): Result<List<LoginAccount>> = wrap {
        val current = session.currentSessionId
        api.loginSessions(session.deviceId).map { s ->
            LoginAccount(
                id = s.loginSessionSeq.toString(),
                userName = s.userName,
                recentlyUsed = s.loginSessionSeq == current,
                canResume = session.hasRefresh(s.loginSessionSeq),
            )
        }.sortedByDescending { it.recentlyUsed }
    }

    override suspend fun loginWithAccount(id: String): Result<LoginOutcome> = wrap {
        val sid = id.toLong()
        when (val r = session.refresh(sid)) {
            is TokenResult.Success -> LoginOutcome(r.token.setupRequired)
            // invalid_grant: 만료(90일)·로그아웃·삭제 → QR 로그인 필요
            is TokenResult.OAuthError -> throw IllegalStateException("이 계정은 다시 로그인이 필요해요. QR을 찍어 로그인해 주세요")
        }
    }

    override suspend fun issueQr(): Result<QrSession> = wrap {
        // 새 QR을 만들 때 이전 QR은 취소(USER)
        deviceCode?.let { old -> runCatching { api.deleteQrLogin(old, session.deviceId, "USER") } }
        val a = api.deviceAuthorization(session.deviceId)
        deviceCode = a.deviceCode
        QrSession(a.verificationUriComplete, a.matchCode, a.expiresIn, a.interval)
    }

    override suspend fun pollQr(): Result<QrPoll> = wrap {
        val code = deviceCode ?: return@wrap QrPoll.Invalid
        when (val r = api.tokenByDeviceCode(code, session.deviceId)) {
            is TokenResult.Success -> {
                deviceCode = null // 같은 device_code는 토큰 1회만 발급
                session.accept(r.token)
                QrPoll.Approved(LoginOutcome(r.token.setupRequired))
            }
            is TokenResult.OAuthError -> when (r.error) {
                "authorization_pending" -> QrPoll.Pending(r.qrStatus ?: "PENDING")
                "slow_down" -> QrPoll.SlowDown
                "expired_token" -> { deviceCode = null; QrPoll.Expired }
                "access_denied" -> { deviceCode = null; QrPoll.Denied(r.cancelReason) }
                else -> { deviceCode = null; QrPoll.Invalid } // invalid_grant · invalid_request
            }
        }
    }

    override suspend fun cancelQr(reason: String): Result<Unit> = wrap {
        val code = deviceCode ?: return@wrap
        deviceCode = null
        api.deleteQrLogin(code, session.deviceId, reason)
        Unit
    }

    override suspend fun requestOtp(phoneDigits: String): Result<OtpRequest> = wrap {
        val r = api.addPhoneVerification(phoneDigits)
        when (r.code) {
            101 -> OtpRequest.TooSoon(r.retryAfter)
            102 -> OtpRequest.Locked(r.retryAfter)
            // 문자 발송 실패도 HTTP 200일 수 있다(명세) → 성공 응답만으로 도착을 보장하지 않음
            else -> OtpRequest.Sent(r.expiresIn.takeIf { it > 0 } ?: 180, r.retryAfter)
        }
    }

    override suspend fun verifyOtp(phoneDigits: String, code: String): Result<OtpVerify> = wrap {
        when (val r = api.tokenByOtp(phoneDigits, code, session.deviceId)) {
            is TokenResult.Success -> {
                session.accept(r.token)
                OtpVerify.Success(LoginOutcome(r.token.setupRequired))
            }
            is TokenResult.OAuthError -> when (r.error) {
                "otp_mismatch" -> OtpVerify.Mismatch(r.remainingAttempts)
                "otp_locked" -> OtpVerify.Locked(r.retryAfter)
                "otp_expired" -> OtpVerify.Expired
                "invalid_grant" -> OtpVerify.NotIssued
                "signup_required" -> OtpVerify.SignupRequired
                else -> throw IllegalStateException("입력 형식을 확인해 주세요")
            }
        }
    }

    override suspend fun logoutThisCar(): Result<Unit> = wrap {
        val sid = session.currentSessionId ?: return@wrap
        val token = session.currentAccessToken() ?: (session.refresh(sid) as? TokenResult.Success)?.token?.accessToken
        // 서버 로그아웃 실패(네트워크 등)여도 이 차의 토큰은 지운다(명세: 앱에서 토큰·로컬 데이터 직접 삭제)
        token?.let { runCatching { api.deleteLoginSession(it) } }
        session.forget(sid)
    }

    private inline fun <T> wrap(block: () -> T): Result<T> = try {
        Result.success(block())
    } catch (e: ApiException) {
        Result.failure(IllegalStateException(e.userMessage, e))
    } catch (e: Exception) {
        Result.failure(e)
    }
}
