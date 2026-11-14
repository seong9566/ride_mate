package com.example.pickup_hyundai.core.net

import android.util.Log
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import org.json.JSONObject
import java.io.IOException
import java.net.HttpURLConnection
import java.net.URL
import java.net.URLEncoder

/** 서버 연결 설정. 값은 local.properties → BuildConfig(커밋 안 됨). 비어 있으면 Mock 저장소를 쓴다. */
data class ApiConfig(val baseUrl: String, val accessToken: String) {
    val isConfigured: Boolean get() = baseUrl.isNotBlank() && accessToken.isNotBlank()
}

/** 정본 공통 응답 {message, data, code} */
data class ApiEnvelope(val code: Int, val message: String?, val data: JSONObject?)

enum class ApiErrorKind { BAD_REQUEST, UNAUTHORIZED, CONFLICT, RATE_LIMITED, SERVER, NETWORK, PARSE }

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
            ApiErrorKind.UNAUTHORIZED -> "로그인이 만료됐어요. 다시 로그인해 주세요"
            ApiErrorKind.RATE_LIMITED -> "요청이 많아요. 잠시 뒤 다시 시도해 주세요"
            ApiErrorKind.NETWORK -> "서버에 연결하지 못했어요"
            ApiErrorKind.CONFLICT -> when (reason) {
                "TOO_EARLY" -> "아직 픽업 계획을 만들 수 없어요(하원 3시간 전부터)"
                "NO_ACADEMY_LOCATION" -> "학원 위치가 설정되지 않았어요"
                else -> "요청을 처리할 수 없어요"
            }
            else -> "일시적인 오류예요. 다시 시도해 주세요"
        }
}

/**
 * 의존성 추가 없이 HttpURLConnection + org.json으로 만든 최소 클라이언트.
 * - Authorization: Bearer 토큰은 헤더로만 보내고 로그에 남기지 않는다.
 * - 로그에는 메서드·경로·HTTP 상태만 남긴다(쿼리에 차량 좌표가 있어 제외).
 * - 429는 본문을 읽지 않고 Retry-After만 사용한다.
 */
class ApiClient(private val config: ApiConfig) {

    suspend fun get(path: String, query: Map<String, Any?>): ApiEnvelope =
        request("GET", path + buildQuery(query), body = null)

    suspend fun post(path: String, body: JSONObject): ApiEnvelope =
        request("POST", path, body.toString())

    private suspend fun request(method: String, pathAndQuery: String, body: String?): ApiEnvelope = withContext(Dispatchers.IO) {
        val url = URL(config.baseUrl.trimEnd('/') + pathAndQuery)
        val conn = try {
            url.openConnection() as HttpURLConnection
        } catch (e: IOException) {
            throw ApiException(ApiErrorKind.NETWORK, null)
        }
        try {
            conn.requestMethod = method
            conn.connectTimeout = TIMEOUT_MS
            conn.readTimeout = TIMEOUT_MS
            conn.setRequestProperty("Accept", "application/json")
            conn.setRequestProperty("Authorization", "Bearer ${config.accessToken}")
            if (body != null) {
                conn.doOutput = true
                conn.setRequestProperty("Content-Type", "application/json")
                conn.outputStream.use { it.write(body.toByteArray(Charsets.UTF_8)) }
            }
            val status = conn.responseCode
            Log.d(TAG, "$method ${pathAndQuery.substringBefore('?')} → $status")
            if (status == 429) {
                throw ApiException(ApiErrorKind.RATE_LIMITED, status, retryAfterSec = conn.getHeaderField("Retry-After")?.toLongOrNull())
            }
            val text = (if (status in 200..299) conn.inputStream else conn.errorStream)
                ?.bufferedReader(Charsets.UTF_8)?.use { it.readText() }.orEmpty()
            val json = runCatching { JSONObject(text) }.getOrNull()
            val data = json?.optJSONObject("data")
            when {
                status in 200..299 && json != null ->
                    ApiEnvelope(json.optInt("code", status), json.optString("message").ifBlank { null }, data)
                status in 200..299 -> throw ApiException(ApiErrorKind.PARSE, status)
                status == 400 || status == 415 -> throw ApiException(ApiErrorKind.BAD_REQUEST, status)
                status == 401 -> throw ApiException(ApiErrorKind.UNAUTHORIZED, status)
                status == 409 -> throw ApiException(ApiErrorKind.CONFLICT, status, reason = data?.optString("reason")?.ifBlank { null })
                else -> throw ApiException(ApiErrorKind.SERVER, status)
            }
        } catch (e: ApiException) {
            throw e
        } catch (e: IOException) {
            Log.w(TAG, "$method ${pathAndQuery.substringBefore('?')} network error: ${e.javaClass.simpleName}")
            throw ApiException(ApiErrorKind.NETWORK, null)
        } finally {
            conn.disconnect()
        }
    }

    private fun buildQuery(query: Map<String, Any?>): String {
        val parts = query.filterValues { it != null }.map { (k, v) ->
            "${URLEncoder.encode(k, "UTF-8")}=${URLEncoder.encode(v.toString(), "UTF-8")}"
        }
        return if (parts.isEmpty()) "" else "?" + parts.joinToString("&")
    }

    private companion object {
        const val TAG = "WayMe/Api"
        const val TIMEOUT_MS = 10_000
    }
}
