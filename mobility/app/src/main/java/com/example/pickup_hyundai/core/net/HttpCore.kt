package com.example.pickup_hyundai.core.net

import android.util.Log
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import org.json.JSONObject
import java.io.IOException
import java.net.HttpURLConnection
import java.net.URL
import java.net.URLEncoder

/** HTTP 응답 원형. 본문 형식이 API마다 달라(OAuth2 원형 · {message,data,code} · ASP.NET 검증 오류) 해석은 호출부가 한다 */
data class HttpResponse(val status: Int, val body: String, val retryAfterSec: Long?) {
    val json: JSONObject? get() = runCatching { JSONObject(body) }.getOrNull()
}

/**
 * 의존성 추가 없이 HttpURLConnection으로 만든 최소 HTTP 호출기.
 * - 로그에는 메서드·경로·HTTP 상태만 남긴다(쿼리·본문에 좌표·전화번호·토큰이 있을 수 있어 제외).
 * - 429는 본문을 읽지 않고 Retry-After만 넘긴다.
 * @throws ApiException(NETWORK) 연결 실패
 */
class HttpCore(private val baseUrl: String) {

    val isConfigured: Boolean get() = baseUrl.isNotBlank()

    suspend fun send(
        method: String,
        path: String,
        query: Map<String, Any?> = emptyMap(),
        json: JSONObject? = null,
        form: Map<String, String>? = null,
        bearer: String? = null,
    ): HttpResponse = withContext(Dispatchers.IO) {
        val url = URL(baseUrl.trimEnd('/') + path + buildQuery(query))
        val conn = try { url.openConnection() as HttpURLConnection } catch (e: IOException) { throw ApiException(ApiErrorKind.NETWORK, null) }
        try {
            conn.requestMethod = method
            conn.connectTimeout = TIMEOUT_MS
            conn.readTimeout = TIMEOUT_MS
            conn.setRequestProperty("Accept", "application/json")
            bearer?.let { conn.setRequestProperty("Authorization", "Bearer $it") }
            val body: String? = when {
                form != null -> {
                    conn.setRequestProperty("Content-Type", "application/x-www-form-urlencoded")
                    form.entries.joinToString("&") { (k, v) -> "${enc(k)}=${enc(v)}" }
                }
                json != null -> {
                    conn.setRequestProperty("Content-Type", "application/json")
                    json.toString()
                }
                else -> null
            }
            if (body != null) {
                conn.doOutput = true
                conn.outputStream.use { it.write(body.toByteArray(Charsets.UTF_8)) }
            }
            val status = conn.responseCode
            Log.d(TAG, "$method $path → $status")
            if (status == 429) return@withContext HttpResponse(status, "", conn.getHeaderField("Retry-After")?.toLongOrNull())
            val text = (if (status in 200..299) conn.inputStream else conn.errorStream)
                ?.bufferedReader(Charsets.UTF_8)?.use { it.readText() }.orEmpty()
            HttpResponse(status, text, conn.getHeaderField("Retry-After")?.toLongOrNull())
        } catch (e: IOException) {
            Log.w(TAG, "$method $path network error: ${e.javaClass.simpleName}")
            throw ApiException(ApiErrorKind.NETWORK, null)
        } finally {
            conn.disconnect()
        }
    }

    private fun enc(s: String) = URLEncoder.encode(s, "UTF-8")

    private fun buildQuery(query: Map<String, Any?>): String {
        val parts = query.filterValues { it != null }.map { (k, v) -> "${enc(k)}=${enc(v.toString())}" }
        return if (parts.isEmpty()) "" else "?" + parts.joinToString("&")
    }

    private companion object {
        const val TAG = "WayMe/Api"
        const val TIMEOUT_MS = 10_000
    }
}
