package com.example.pickup_hyundai.core.net

import org.json.JSONObject
import java.io.IOException

/** 일반 API 공통 응답 {message, data, code} */
data class ApiEnvelope(val code: Int, val message: String?, val data: JSONObject?)

enum class ApiErrorKind { BAD_REQUEST, UNAUTHORIZED, CONFLICT, NOT_FOUND, RATE_LIMITED, SERVER, NETWORK, PARSE }

class ApiException(
    val kind: ApiErrorKind,
    val httpStatus: Int?,
    /** 409일 때 data.reason (예: TOO_EARLY, NO_ACADEMY_LOCATION) */
    val reason: String? = null,
    val retryAfterSec: Long? = null,
) : IOException("API ${kind.name}${httpStatus?.let { " ($it)" } ?: ""}${reason?.let { " $it" } ?: ""}") {
    /** 화면 문구 */
    val userMessage: String
        get() = when (kind) {
            ApiErrorKind.UNAUTHORIZED -> "로그인이 필요해요. 다시 로그인해 주세요"
            ApiErrorKind.RATE_LIMITED -> "요청이 많아요. ${retryAfterSec?.let { "${it}초 " } ?: "잠시 "}뒤 다시 시도해 주세요"
            ApiErrorKind.NETWORK -> "서버에 연결하지 못했어요"
            ApiErrorKind.NOT_FOUND -> "요청한 정보를 찾을 수 없어요"
            ApiErrorKind.CONFLICT -> when (reason) {
                "TOO_EARLY" -> "아직 픽업 계획을 만들 수 없어요(하원 3시간 전부터)"
                "NO_ACADEMY_LOCATION" -> "학원 위치가 설정되지 않았어요"
                else -> "요청을 처리할 수 없어요"
            }
            else -> "일시적인 오류예요. 다시 시도해 주세요"
        }
}

/** Bearer 토큰 공급자(로그인 세션). 401이면 refreshAfterUnauthorized로 한 번 갱신 후 재시도 */
interface TokenSource {
    suspend fun accessToken(): String?
    suspend fun refreshAfterUnauthorized(): String?
}

/**
 * 차량 Bearer API용 클라이언트({message,data,code} 응답).
 * 토큰이 없으면 401로 처리, 401 응답이면 토큰을 갱신해 한 번만 다시 보낸다.
 */
class ApiClient(private val http: HttpCore, private val tokens: TokenSource) {

    suspend fun get(path: String, query: Map<String, Any?>): ApiEnvelope = call { t -> http.send("GET", path, query = query, bearer = t) }

    suspend fun post(path: String, body: JSONObject): ApiEnvelope = call { t -> http.send("POST", path, json = body, bearer = t) }

    /** HTTP 응답 원형이 필요한 API용(예: 409 본문을 정상 흐름으로 쓰는 AddChild). 토큰·401 재시도는 같다 */
    suspend fun postRaw(path: String, body: JSONObject): HttpResponse = raw { t -> http.send("POST", path, json = body, bearer = t) }

    private suspend fun call(block: suspend (String) -> HttpResponse): ApiEnvelope = envelope(raw(block))

    private suspend fun raw(block: suspend (String) -> HttpResponse): HttpResponse {
        val token = tokens.accessToken() ?: throw ApiException(ApiErrorKind.UNAUTHORIZED, null)
        var res = block(token)
        if (res.status == 401) {
            val renewed = tokens.refreshAfterUnauthorized() ?: throw ApiException(ApiErrorKind.UNAUTHORIZED, 401)
            res = block(renewed)
        }
        return res
    }

    companion object {
        /** {message,data,code} 해석 + HTTP 오류 → ApiException */
        fun envelope(res: HttpResponse): ApiEnvelope {
            val json = res.json
            val data = json?.optJSONObject("data")
            return when {
                res.status in 200..299 && json != null ->
                    ApiEnvelope(json.optInt("code", res.status), json.optString("message").ifBlank { null }, data)
                res.status in 200..299 -> throw ApiException(ApiErrorKind.PARSE, res.status)
                res.status == 429 -> throw ApiException(ApiErrorKind.RATE_LIMITED, 429, retryAfterSec = res.retryAfterSec)
                res.status == 400 || res.status == 415 -> throw ApiException(ApiErrorKind.BAD_REQUEST, res.status)
                res.status == 401 -> throw ApiException(ApiErrorKind.UNAUTHORIZED, 401)
                res.status == 404 -> throw ApiException(ApiErrorKind.NOT_FOUND, 404)
                res.status == 409 -> throw ApiException(ApiErrorKind.CONFLICT, 409, reason = data?.optString("reason")?.ifBlank { null })
                else -> throw ApiException(ApiErrorKind.SERVER, res.status)
            }
        }
    }
}
