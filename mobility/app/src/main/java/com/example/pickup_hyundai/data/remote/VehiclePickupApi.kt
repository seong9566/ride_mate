package com.example.pickup_hyundai.data.remote

import com.example.pickup_hyundai.core.net.ApiClient
import com.example.pickup_hyundai.domain.route.GeoPoint
import org.json.JSONArray
import org.json.JSONObject
import java.time.LocalDateTime
import java.time.LocalTime
import java.time.format.DateTimeFormatter

/** 정본 B-1 DetailDepartureGuide 응답(data) */
data class DepartureGuide(
    val academyName: String?,
    val endTime: LocalTime?,
    val departureDt: LocalDateTime?,
    val slackMin: Int?,
    val childArrivalDt: LocalDateTime?,
    val driveMin: Int?,
    /** NAVI · ESTIMATE */
    val driveSource: String?,
    val pickup: GuidePickup?,
    /** RESTRICTED · CLEAR · UNKNOWN */
    val enforcementStatus: String?,
    val reasons: List<String>,
    val pickupPlanSeq: Long?,
    val slotDt: LocalDateTime?,
    val planStatus: String?,
)

data class GuidePickup(
    val pickupSpotSeq: Int?,
    val spotName: String?,
    val position: GeoPoint,
    val walkMin: Int?,
    val walkSource: String?,
    /** 학원 → 픽업 장소 도보 경로(자녀 GPS 경로 아님) */
    val walkPath: List<GeoPoint>,
)

/** 정본 B-2 AddPickupPlan 응답(data) */
data class PickupPlan(
    val pickupPlanSeq: Long,
    val status: String?,
    val academyName: String?,
    val endTime: LocalTime?,
    val pickup: PlanPickup?,
    val slotDt: LocalDateTime?,
    val expireDt: LocalDateTime?,
)

data class PlanPickup(
    /** SPOT · CUSTOM */
    val locationType: String?,
    val pickupSpotSeq: Int?,
    val name: String?,
    val position: GeoPoint,
    val walkMin: Int?,
    val guideText: String?,
    val photoUrl: String?,
)

/**
 * 차량 전용 API 2종(정본 B부). 이 두 개 외의 차량 API는 정본에 없어 호출하지 않는다.
 * 반환값 null = HTTP 200 + code 101(오늘 남은 픽업 없음).
 */
class VehiclePickupApi(private val client: ApiClient) {

    suspend fun detailDepartureGuide(
        currentDt: LocalDateTime,
        current: GeoPoint,
        driveMin: Int?,
        pickup: GeoPoint?,
    ): DepartureGuide? {
        val env = client.get(
            "/api/v1/Pickup/M/sign/DetailDepartureGuide",
            linkedMapOf(
                "currentDt" to currentDt.withNano(0).format(KST),
                "currentLat" to current.lat,
                "currentLng" to current.lng,
                "driveMin" to driveMin,
                // 두 값은 함께 보내거나 함께 뺀다(정본)
                "pickupLat" to pickup?.lat,
                "pickupLng" to pickup?.lng,
            ),
        )
        if (env.code == CODE_NO_PICKUP) return null
        return env.data?.toGuide()
    }

    /** 기본 지점이면 빈 객체 {}를 보낸다(본문 생략 시 400). 이미 계획이 있으면 기존 계획 반환(멱등). */
    suspend fun addPickupPlan(pickup: GeoPoint?): PickupPlan? {
        val body = JSONObject()
        if (pickup != null) {
            body.put("pickupLat", pickup.lat)
            body.put("pickupLng", pickup.lng)
        }
        val env = client.post("/api/v1/Pickup/M/sign/AddPickupPlan", body)
        if (env.code == CODE_NO_PICKUP) return null
        return env.data?.toPlan()
    }

    private fun JSONObject.toGuide() = DepartureGuide(
        academyName = str("academyName"),
        endTime = str("endTime")?.let { runCatching { LocalTime.parse(it) }.getOrNull() },
        departureDt = dt("departureDt"),
        slackMin = int("slackMin"),
        childArrivalDt = dt("childArrivalDt"),
        driveMin = int("driveMin"),
        driveSource = str("driveSource"),
        pickup = optJSONObject("pickup")?.let { p ->
            GuidePickup(
                pickupSpotSeq = p.int("pickupSpotSeq"),
                spotName = p.str("spotName"),
                position = GeoPoint(p.getDouble("lat"), p.getDouble("lng")),
                walkMin = p.int("walkMin"),
                walkSource = p.str("walkSource"),
                walkPath = p.optJSONArray("walkPath").points(),
            )
        },
        enforcementStatus = optJSONObject("enforcement")?.str("status"),
        reasons = optJSONArray("reasons").strings(),
        pickupPlanSeq = long("pickupPlanSeq"),
        slotDt = dt("slotDt"),
        planStatus = str("planStatus"),
    )

    private fun JSONObject.toPlan() = PickupPlan(
        pickupPlanSeq = getLong("pickupPlanSeq"),
        status = str("status"),
        academyName = str("academyName"),
        endTime = str("endTime")?.let { runCatching { LocalTime.parse(it) }.getOrNull() },
        pickup = optJSONObject("pickup")?.let { p ->
            PlanPickup(
                locationType = p.str("locationType"),
                pickupSpotSeq = p.int("pickupSpotSeq"),
                name = p.str("name"),
                position = GeoPoint(p.getDouble("lat"), p.getDouble("lng")),
                walkMin = p.int("walkMin"),
                guideText = p.str("guideText"),
                photoUrl = p.str("photoUrl"),
            )
        },
        slotDt = dt("slotDt"),
        expireDt = dt("expireDt"),
    )

    private fun JSONObject.str(k: String): String? = if (isNull(k)) null else optString(k).ifBlank { null }
    private fun JSONObject.int(k: String): Int? = if (isNull(k) || !has(k)) null else optInt(k)
    private fun JSONObject.long(k: String): Long? = if (isNull(k) || !has(k)) null else optLong(k)
    private fun JSONObject.dt(k: String): LocalDateTime? = str(k)?.let { runCatching { LocalDateTime.parse(it) }.getOrNull() }

    private fun JSONArray?.points(): List<GeoPoint> = if (this == null) emptyList() else
        (0 until length()).mapNotNull { i -> optJSONObject(i)?.let { GeoPoint(it.getDouble("lat"), it.getDouble("lng")) } }

    /** reasons 원소 형식이 문서에 없어 문자열이면 그대로, 객체면 code/type 필드 순으로 읽는다(백엔드 확인 필요) */
    private fun JSONArray?.strings(): List<String> = if (this == null) emptyList() else
        (0 until length()).mapNotNull { i ->
            when (val v = opt(i)) {
                is String -> v
                is JSONObject -> v.optString("code").ifBlank { v.optString("type") }.ifBlank { null }
                else -> null
            }
        }

    private companion object {
        const val CODE_NO_PICKUP = 101
        val KST: DateTimeFormatter = DateTimeFormatter.ofPattern("yyyy-MM-dd'T'HH:mm:ss")
    }
}
