package com.example.pickup_hyundai.data.remote

import com.example.pickup_hyundai.core.net.ApiClient
import com.example.pickup_hyundai.domain.model.AcademyInput
import org.json.JSONArray
import org.json.JSONObject
import java.time.DayOfWeek
import java.time.LocalTime

/** AddSchedule 응답의 schedule(시간표 목록 항목과 같은 모양) */
data class ServerSchedule(
    val scheduleSeq: Long,
    val displayName: String,
    val academyName: String,
    val address: String,
    val endTime: LocalTime,
    val days: List<ServerScheduleDay>,
    /** 우리 픽업 장소(후보 없으면 null) */
    val pickupSpotName: String?,
    val pickupWalkMin: Int?,
)

data class ServerScheduleDay(val scheduleDaySeq: Long, val day: DayOfWeek, val pickupYn: Boolean)

/** code 200 저장됨 / 101 아이 정보 없음 / 102 10개 초과 / 103 시간이 가까운 시간표 / 104 학원가 밖 (모두 HTTP 200) */
data class AddScheduleResult(val code: Int, val message: String?, val schedule: ServerSchedule?)

/**
 * 시간표 API — V01-3 초기 설정 · 학원 시간표 추가(2026-11-15 전달 명세).
 * POST /api/v1/Schedule/M/sign/AddSchedule (Bearer)
 */
class ScheduleApi(private val client: ApiClient) {

    suspend fun addSchedule(input: AcademyInput): AddScheduleResult {
        val place = JSONObject().put("name", input.placeName.trim()).put("address", input.address.trim())
        // 좌표는 둘 다 보내거나 둘 다 뺀다
        if (input.latitude != null && input.longitude != null) place.put("lat", input.latitude).put("lng", input.longitude)
        val body = JSONObject()
            .put("displayName", input.displayName.trim())
            .put("place", place)
            .put("days", JSONArray(input.days.map { it.value }.sorted())) // 1(월)~7(일)
            .put("endTime", "%02d:%02d".format(input.endTime.hour, input.endTime.minute))
            .put("confirmOverlap", false) // 2차 개발 항목 → false 고정(명세)
        val env = client.post("/api/v1/Schedule/M/sign/AddSchedule", body)
        val s = env.data?.optJSONObject("schedule")
        return AddScheduleResult(env.code, env.message, s?.let(::parse))
    }

    private fun parse(s: JSONObject): ServerSchedule {
        val arr = s.optJSONArray("days") ?: JSONArray()
        val spot = s.optJSONObject("pickupSpot")
        return ServerSchedule(
            scheduleSeq = s.getLong("scheduleSeq"),
            displayName = s.optString("displayName"),
            academyName = s.optString("academyName"),
            address = s.optString("address"),
            endTime = LocalTime.parse(s.getString("endTime")),
            days = (0 until arr.length()).mapNotNull { i ->
                arr.optJSONObject(i)?.let { ServerScheduleDay(it.getLong("scheduleDaySeq"), DayOfWeek.of(it.getInt("dayOfWeek")), it.optBoolean("pickupYn", true)) }
            },
            pickupSpotName = spot?.optString("spotName")?.ifBlank { null },
            pickupWalkMin = spot?.takeIf { it.has("walkMin") && !it.isNull("walkMin") }?.optInt("walkMin"),
        )
    }
}
