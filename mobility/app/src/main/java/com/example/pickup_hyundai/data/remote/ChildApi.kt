package com.example.pickup_hyundai.data.remote

import com.example.pickup_hyundai.core.net.ApiClient
import com.example.pickup_hyundai.domain.model.ChildInfoInput
import org.json.JSONObject

/**
 * code 200 저장됨 / 409 이미 아이가 있음(저장된 아이가 data로 옴 → 다음 단계로) /
 * 102 부모 번호와 같음 · 103 다른 계정에 등록된 번호(HTTP 200, message를 그대로 표시)
 */
data class AddChildResult(val code: Int, val message: String?, val childName: String?)

/**
 * 아이 정보 API — V01-2 아이 정보(2026-11-15 전달 명세).
 * POST /api/v1/ChildInfo/M/sign/AddChild (Bearer). 계정당 아이는 한 명.
 */
class ChildApi(private val client: ApiClient) {

    suspend fun addChild(child: ChildInfoInput): AddChildResult {
        val b = child.birthDigits // YYYYMMDD → yyyy.MM.dd
        val body = JSONObject()
            .put("childName", child.name.trim())
            .put("phone", child.phoneDigits)
            .put("sendChannel", "SMS") // 개발 단계는 SMS 고정(명세)
            .put("birthDate", "${b.substring(0, 4)}.${b.substring(4, 6)}.${b.substring(6, 8)}")
        // 409는 오류가 아니라 "이미 저장됨"이라 본문을 직접 읽는다
        val res = client.postRaw("/api/v1/ChildInfo/M/sign/AddChild", body)
        if (res.status == 409) {
            val j = res.json
            return AddChildResult(409, j?.optString("message")?.ifBlank { null }, j?.optJSONObject("data")?.optString("childName")?.ifBlank { null })
        }
        val env = ApiClient.envelope(res)
        return AddChildResult(env.code, env.message, env.data?.optString("childName")?.ifBlank { null })
    }
}
