package com.example.pickup_hyundai.data.repository

import com.example.pickup_hyundai.core.net.ApiException
import com.example.pickup_hyundai.core.time.KstFormat
import com.example.pickup_hyundai.data.remote.DepartureGuide
import com.example.pickup_hyundai.data.remote.VehiclePickupApi
import com.example.pickup_hyundai.domain.model.AssignedPlan
import com.example.pickup_hyundai.domain.model.DeparturePlan
import com.example.pickup_hyundai.domain.model.PickupItem
import com.example.pickup_hyundai.domain.model.PickupSpotChoice
import com.example.pickup_hyundai.domain.model.TodayPickupDetail
import com.example.pickup_hyundai.domain.model.WaitingPlaceChoice
import com.example.pickup_hyundai.domain.route.GeoPoint
import java.time.Duration
import java.time.LocalDateTime

/** HTTP 200 + code 101: 오늘 남은 픽업이 없다 */
class NoPickupTodayException : Exception("오늘 남은 픽업이 없어요")

/**
 * 정본 차량 API 2종(DetailDepartureGuide, AddPickupPlan)으로 채울 수 있는 값은 서버에서, 나머지는 [fallback](Mock)에서 가져온다.
 * - 정본에 없는 값: 픽업 후보 2·3번째·태그, 대기 장소, 학원 근처 → 집 소요 → Mock 유지(isMock = true로 표시)
 * - 차량 위치(FusedLocation)가 없으면 필수 쿼리(currentLat/Lng)를 채울 수 없으므로 호출하지 않고 안내한다(가짜 좌표 금지).
 */
class RemotePickupRepository(
    private val api: VehiclePickupApi,
    private val fallback: MockPickupRepository,
    /** FusedLocation 최신 위치 */
    private val currentLocation: () -> GeoPoint?,
    /** NaviHelper TripInfo로 구한 픽업 장소까지 소요(분). 없으면 서버 추정 */
    private val naviDriveMin: () -> Int?,
    private val childName: () -> String?,
) : PickupRepository {

    @Volatile private var cachedGuide: Pair<LocalDateTime, DepartureGuide>? = null
    @Volatile private var plan: AssignedPlan? = null
    /** 부모가 V04에서 고른 좌표. 계획이 생기기 전에는 DetailDepartureGuide pickupLat/Lng로 보낸다 */
    @Volatile private var chosenPickup: GeoPoint? = null

    private suspend fun guide(maxAgeSec: Long = 30): DepartureGuide {
        val now = LocalDateTime.now(KstFormat.ZONE)
        cachedGuide?.let { (at, g) -> if (Duration.between(at, now).seconds < maxAgeSec) return g }
        val loc = currentLocation() ?: throw IllegalStateException("차량 위치를 확인하는 중이에요. 잠시 뒤 다시 시도해 주세요")
        val g = api.detailDepartureGuide(now, loc, naviDriveMin(), if (plan == null) chosenPickup else null)
            ?: throw NoPickupTodayException()
        cachedGuide = now to g
        maybeAutoPlan(now, g)
        return g
    }

    /** 정본 B-2: 추천 출발 30분 전(이미 30분 안이면 즉시) 자동 호출 */
    private suspend fun maybeAutoPlan(now: LocalDateTime, g: DepartureGuide) {
        if (plan != null || g.pickupPlanSeq != null) return
        val dep = g.departureDt ?: return
        if (Duration.between(now, dep).toMinutes() <= 30) runCatching { ensurePickupPlan(chosenPickup) }
    }

    override suspend fun getTodayPickup(): Result<TodayPickupDetail> = wrap {
        val g = guide()
        TodayPickupDetail(
            item = PickupItem(childName() ?: "아이", g.academyName ?: "학원", g.endTime ?: error("하원 시각 없음"), g.pickup?.spotName),
            areaName = null,
            driveToPickupMin = g.driveMin,
            pickupToHomeMin = null, // 정본 API 없음
            homeName = "집",
            homeArrivalTime = null,
            isMock = false,
        )
    }

    override suspend fun getPickupSpotChoice(): Result<PickupSpotChoice> = wrap {
        val g = guide()
        val mock = fallback.getPickupSpotChoice().getOrThrow()
        val p = g.pickup
        // 정본은 추천 장소 1곳만 준다 → 1번 후보를 서버 값으로, 2·3번은 Mock(MOCK 표시 유지)
        val first = mock.candidates.first().let { c ->
            if (p == null) c else c.copy(name = p.spotName ?: c.name, walkMin = p.walkMin, latitude = p.position.lat, longitude = p.position.lng)
        }
        mock.copy(
            candidates = listOf(first) + mock.candidates.drop(1),
            departureDt = g.departureDt,
            arrivalDt = g.childArrivalDt,
            isMock = true,
        )
    }

    override suspend fun getDeparturePlan(): Result<DeparturePlan> = wrap {
        val g = guide(maxAgeSec = 0)
        val p = g.pickup ?: throw IllegalStateException(
            if ("NO_ACADEMY_LOCATION" in g.reasons) "학원 위치가 설정되지 않았어요" else "배정할 수 있는 픽업 장소가 없어요"
        )
        DeparturePlan(
            departureDt = g.departureDt ?: error("추천 출발 시각 없음"),
            arrivalDt = g.childArrivalDt,
            originName = "현재 위치",
            pickupName = plan?.pickupName ?: p.spotName ?: "픽업 장소",
            academyName = g.academyName ?: "학원",
            endTime = g.endTime ?: error("하원 시각 없음"),
            walkMin = p.walkMin,
            pickupLatitude = p.position.lat,
            pickupLongitude = p.position.lng,
            totalMin = null,        // 정본 없음(학원 근처 → 집)
            homeArrivalTime = null, // 정본 없음
            isMock = false,
            enforcementRestricted = when (g.enforcementStatus) { "RESTRICTED" -> true; "CLEAR" -> false; else -> null },
            slotDt = g.slotDt,
        )
    }

    override suspend fun getWaitingPlaces(): Result<WaitingPlaceChoice> = fallback.getWaitingPlaces()

    override suspend fun ensurePickupPlan(pickup: GeoPoint?): Result<AssignedPlan?> = wrap {
        if (pickup != null) chosenPickup = pickup
        plan?.let { return@wrap it }
        val r = api.addPickupPlan(pickup) ?: return@wrap null
        AssignedPlan(
            pickupPlanSeq = r.pickupPlanSeq,
            pickupName = r.pickup?.name,
            latitude = r.pickup?.position?.lat,
            longitude = r.pickup?.position?.lng,
            walkMin = r.pickup?.walkMin,
            slotDt = r.slotDt,
            guideText = r.pickup?.guideText,
            isMock = false,
        ).also { plan = it; cachedGuide = null }
    }

    private inline fun <T> wrap(block: () -> T): Result<T> = try {
        Result.success(block())
    } catch (e: ApiException) {
        Result.failure(IllegalStateException(e.userMessage, e))
    } catch (e: Exception) {
        Result.failure(e)
    }
}
