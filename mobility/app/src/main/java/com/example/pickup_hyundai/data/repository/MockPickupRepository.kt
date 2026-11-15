package com.example.pickup_hyundai.data.repository

import com.example.pickup_hyundai.domain.model.AssignedPlan
import com.example.pickup_hyundai.domain.model.DeparturePlan
import com.example.pickup_hyundai.domain.model.PickupItem
import com.example.pickup_hyundai.domain.model.PickupSpotCandidate
import com.example.pickup_hyundai.domain.model.PickupSpotChoice
import com.example.pickup_hyundai.domain.model.SpotTag
import com.example.pickup_hyundai.domain.model.TodayPickupDetail
import com.example.pickup_hyundai.domain.model.WaitingPlace
import com.example.pickup_hyundai.domain.model.WaitingPlaceChoice
import com.example.pickup_hyundai.domain.route.GeoPoint
import java.time.Clock
import java.time.LocalDate
import java.time.LocalTime
import java.time.ZoneId

/** 오늘 첫 픽업(초기 설정에서 등록한 시간표 기준) */
data class TodayBase(val childName: String, val academyName: String, val endTime: LocalTime)

/**
 * 서버 없이 쓰는 픽업 데이터(로컬 계산값 · 예시 장소).
 * - 학원 이름·하원 시각·아이 이름: 초기 설정에서 등록한 오늘 시간표([today])를 그대로 쓴다. 없으면 디자인 예시(수학학원 22:00).
 * - 시각은 하원 시각 기준 계산: 추천 출발 = 하원 − 31분, 배정 도착 = 하원 + 4분, 집 도착 = 하원 + 25분(디자인 예시 간격).
 * - 장소(픽업 후보·대기 장소)와 소요 시간은 시연 경로(대치동, DemoScenario)에 맞춘 예시 값이다 → 서버 API 연결 시 교체.
 */
class MockPickupRepository(
    private val clock: Clock = Clock.system(ZoneId.of("Asia/Seoul")),
    private val today: () -> TodayBase? = { null },
) : PickupRepository {

    private fun base() = today() ?: TodayBase("지민", "수학학원", LocalTime.of(22, 0))
    private fun date(): LocalDate = LocalDate.now(clock)
    private fun departure(b: TodayBase) = date().atTime(b.endTime).minusMinutes(31)
    private fun slot(b: TodayBase) = date().atTime(b.endTime).plusMinutes(4)

    override suspend fun getTodayPickup(): Result<TodayPickupDetail> = runCatching {
        val b = base()
        TodayPickupDetail(
            item = PickupItem(b.childName, b.academyName, b.endTime, null),
            areaName = "대치동 학원가",
            driveToPickupMin = 32,
            pickupToHomeMin = 18,
            homeName = "우리 집",
            homeArrivalTime = b.endTime.plusMinutes(25),
            isMock = true,
        )
    }

    override suspend fun getPickupSpotChoice(): Result<PickupSpotChoice> = runCatching {
        val b = base()
        val hour = b.endTime.hour
        PickupSpotChoice(
            candidates = listOf(
                PickupSpotCandidate(1, "△△빌딩 앞", SpotTag.RECOMMENDED, 5, "학원 앞 공사로 덜 붐벼요", 37.49460, 127.06200),
                PickupSpotCandidate(2, "○○ 앞", SpotTag.LAST_TIME, 3, "오늘 ${hour}시 붐빔", 37.49545, 127.06050),
                PickupSpotCandidate(3, "□□공원 입구", null, 6, "길 건너야 해요", 37.49395, 127.06085),
            ),
            basisText = "오늘 ${hour}시 학원가 상황을 보고 골랐어요",
            departureDt = departure(b),
            arrivalDt = slot(b),
            isMock = true,
        )
    }

    override suspend fun getDeparturePlan(): Result<DeparturePlan> = runCatching {
        val b = base()
        DeparturePlan(
            departureDt = departure(b),
            arrivalDt = slot(b),
            originName = "현재 위치",
            pickupName = "△△빌딩 앞",
            academyName = b.academyName,
            endTime = b.endTime,
            walkMin = 5,
            // 시연 경로(DemoScenario.PICKUP)와 같은 좌표
            pickupLatitude = 37.49460,
            pickupLongitude = 127.06200,
            totalMin = 50,
            homeArrivalTime = b.endTime.plusMinutes(25),
            isMock = true,
            // 디자인 V08 예시: 학원 앞 단속 구역 → 걸어서 갈 수 있는 장소로 안내
            enforcementRestricted = true,
        )
    }

    override suspend fun ensurePickupPlan(pickup: GeoPoint?): Result<AssignedPlan?> = runCatching {
        AssignedPlan(
            pickupPlanSeq = null,
            // 기본 지점 배정(좌표 없음)일 때만 이름이 있다. 부모가 고른 좌표면 고른 후보 이름 유지
            pickupName = if (pickup == null) "△△빌딩 앞" else null,
            latitude = pickup?.lat ?: 37.49460,
            longitude = pickup?.lng ?: 127.06200,
            walkMin = null,
            slotDt = slot(base()),
            guideText = null,
            isMock = true,
        )
    }

    override suspend fun getWaitingPlaces(): Result<WaitingPlaceChoice> = runCatching {
        WaitingPlaceChoice(
            departureDt = departure(base()),
            places = listOf(
                WaitingPlace(1, "도곡초교 공영주차장", true, 5, "약 2,100원", 120, 37.49360, 127.05780, feeDetail = "기본 600원 · 10분 당 300원"),
                WaitingPlace(2, "대치2문화센터 공영", false, 5, "약 2,100원", 30, 37.49720, 127.06520),
                WaitingPlace(3, "도곡로421 공영", false, 4, "약 2,800원", 10, 37.49610, 127.05920),
            ),
            isMock = true,
        )
    }
}
