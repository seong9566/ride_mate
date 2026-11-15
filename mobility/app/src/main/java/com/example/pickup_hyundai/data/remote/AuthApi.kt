package com.example.pickup_hyundai.data.remote

import com.example.pickup_hyundai.core.net.ApiClient
import com.example.pickup_hyundai.core.net.ApiErrorKind
import com.example.pickup_hyundai.core.net.ApiException
import com.example.pickup_hyundai.core.net.HttpCore
import org.json.JSONObject

/** M-01 응답(OAuth2 원형). deviceCode는 비밀 — 화면·로그·QR에 쓰지 않는다 */
data class DeviceAuthorization(
    val deviceCode: String,
    val verificationUriComplete: String,
    val expiresIn: Int,
    val interval: Int,
    /** 4자리 문자열(앞자리 0 유지) */
    val matchCode: String,
)

/** M-02·M-04·M-08 성공 응답 */
data class TokenResponse(
    val accessToken: String,
    val expiresIn: Int,
    val refreshToken: String,
    val sessionId: Long,
    val setupRequired: Boolean,
)

/** /Auth/M/Token 결과 */
sealed interface TokenResult {
    data class Success(val token: TokenResponse) : TokenResult
    /** HTTP 400 OAuth2 오류. error 값과 추가 필드(snake_case) */
    data class OAuthError(
        val error: String,
        val qrStatus: String? = null,
        val cancelReason: String? = null,
        val remainingAttempts: Int? = null,
        val retryAfter: Long? = null,
    ) : TokenResult
}

data class LoginSessionItem(val loginSessionSeq: Long, val userName: String)

/** M-07 결과: code 200 발송 / 101 재요청 제한 / 102 잠금(모두 HTTP 200 가능) */
data class PhoneVerificationResult(val code: Int, val expiresIn: Int, val retryAfter: Int)

/**
 * 차량(M) 인증 API — WAY:ME 인증 API 명세서(2026-11-15 전달본).
 * Token·DeviceAuthorization은 form-urlencoded, 응답은 OAuth2 원형(래퍼 없음). 나머지는 {message,data,code}.
 */
class AuthApi(private val http: HttpCore) {

    /** M-01 QR 발급 */
    suspend fun deviceAuthorization(deviceId: String): DeviceAuthorization {
        val res = http.send("POST", "/api/v1/Auth/M/DeviceAuthorization", form = mapOf("device_id" to deviceId))
        val j = res.json
        if (res.status != 200 || j == null || !j.has("device_code")) throw httpError(res.status, res.retryAfterSec)
        return DeviceAuthorization(
            deviceCode = j.getString("device_code"),
            verificationUriComplete = j.getString("verification_uri_complete"),
            expiresIn = j.optInt("expires_in", 180),
            interval = j.optInt("interval", 3).coerceAtLeast(1),
            matchCode = j.optString("match_code"),
        )
    }

    /** M-02 QR 승인 확인 */
    suspend fun tokenByDeviceCode(deviceCode: String, deviceId: String) = token(
        mapOf("grant_type" to "urn:ietf:params:oauth:grant-type:device_code", "device_code" to deviceCode, "device_id" to deviceId)
    )

    /** M-04 계정 선택 · access token 갱신(매번 새 refresh token) */
    suspend fun tokenByRefresh(refreshToken: String) = token(mapOf("grant_type" to "refresh_token", "refresh_token" to refreshToken))

    /** M-08 차량 OTP 확인 */
    suspend fun tokenByOtp(phone: String, code: String, deviceId: String) = token(
        mapOf("grant_type" to "otp", "phone" to phone, "code" to code, "device_id" to deviceId)
    )

    private suspend fun token(form: Map<String, String>): TokenResult {
        val res = http.send("POST", "/api/v1/Auth/M/Token", form = form)
        val j = res.json
        if (res.status == 200 && j != null && j.has("access_token")) {
            return TokenResult.Success(
                TokenResponse(
                    accessToken = j.getString("access_token"),
                    expiresIn = j.optInt("expires_in", 3600),
                    refreshToken = j.getString("refresh_token"),
                    sessionId = j.getLong("session_id"),
                    setupRequired = j.optBoolean("setup_required", false),
                )
            )
        }
        if (res.status == 400 && j != null && j.has("error")) {
            return TokenResult.OAuthError(
                error = j.getString("error"),
                qrStatus = j.str("qr_status"),
                cancelReason = j.str("cancel_reason"),
                remainingAttempts = if (j.has("remaining_attempts")) j.optInt("remaining_attempts") else null,
                retryAfter = if (j.has("retry_after")) j.optLong("retry_after") else null,
            )
        }
        throw httpError(res.status, res.retryAfterSec)
    }

    /** M-03 QR 취소. reason: USER(사용자 취소·새 QR) / DRIVING(출발). 404·409는 무시해도 되는 상태라 false 반환 */
    suspend fun deleteQrLogin(deviceCode: String, deviceId: String, reason: String): Boolean {
        val res = http.send(
            "DELETE", "/api/v1/Auth/M/DeleteQrLogin",
            json = JSONObject().put("deviceCode", deviceCode).put("deviceId", deviceId).put("reason", reason),
        )
        return when (res.status) {
            200 -> true
            404, 409 -> false
            else -> throw httpError(res.status, res.retryAfterSec)
        }
    }

    /** M-05 로그아웃(이미 로그아웃한 세션도 200) */
    suspend fun deleteLoginSession(accessToken: String) {
        val res = http.send("DELETE", "/api/v1/Auth/M/sign/DeleteLoginSession", bearer = accessToken)
        if (res.status != 200) throw httpError(res.status, res.retryAfterSec)
    }

    /** M-06 이 차에 로그인한 계정 목록(429 → Retry-After) */
    suspend fun loginSessions(deviceId: String): List<LoginSessionItem> {
        val res = http.send("GET", "/api/v1/Auth/M/GetLoginSessionList", query = mapOf("deviceId" to deviceId))
        if (res.status != 200) throw httpError(res.status, res.retryAfterSec)
        val arr = res.json?.optJSONArray("data") ?: return emptyList()
        return (0 until arr.length()).mapNotNull { i ->
            arr.optJSONObject(i)?.let { LoginSessionItem(it.getLong("loginSessionSeq"), it.optString("userName")) }
        }
    }

    /** M-07 차량 OTP 발송 */
    suspend fun addPhoneVerification(phone: String): PhoneVerificationResult {
        val res = http.send("POST", "/api/v1/Auth/M/AddPhoneVerification", json = JSONObject().put("phone", phone))
        val env = ApiClient.envelope(res)
        return PhoneVerificationResult(env.code, env.data?.optInt("expiresIn") ?: 0, env.data?.optInt("retryAfter") ?: 0)
    }

    private fun httpError(status: Int, retryAfter: Long?) = when (status) {
        400, 415 -> ApiException(ApiErrorKind.BAD_REQUEST, status)
        401 -> ApiException(ApiErrorKind.UNAUTHORIZED, status)
        404 -> ApiException(ApiErrorKind.NOT_FOUND, status)
        409 -> ApiException(ApiErrorKind.CONFLICT, status)
        429 -> ApiException(ApiErrorKind.RATE_LIMITED, status, retryAfterSec = retryAfter)
        else -> ApiException(ApiErrorKind.SERVER, status)
    }

    private fun JSONObject.str(k: String): String? = if (isNull(k)) null else optString(k).ifBlank { null }
}
