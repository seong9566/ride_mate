package com.example.pickup_hyundai.data.repository

import com.example.pickup_hyundai.domain.model.AssignedPlan
import com.example.pickup_hyundai.domain.model.DeparturePlan
import com.example.pickup_hyundai.domain.route.GeoPoint
import com.example.pickup_hyundai.domain.model.PickupItem
import com.example.pickup_hyundai.domain.model.WaitingPlace
import com.example.pickup_hyundai.domain.model.WaitingPlaceChoice
import com.example.pickup_hyundai.domain.model.PickupSpotCandidate
import com.example.pickup_hyundai.domain.model.PickupSpotChoice
import com.example.pickup_hyundai.domain.model.SpotTag
import com.example.pickup_hyundai.domain.model.TodayPickupDetail
import java.time.Clock
import java.time.LocalDate
import java.time.LocalTime
import java.time.ZoneId

/** 디자인 이미지 예시 값 Mock(isMock = true). 좌표는 API 명세서 예시 좌표 주변 값. */
class MockPickupRepository(
    private val clock: Clock = Clock.system(ZoneId.of("Asia/Seoul")),
) : PickupRepository {

    override suspend fun getTodayPickup(): Result<TodayPickupDetail> = runCatching {
        TodayPickupDetail(
            item = PickupItem("지민", "수학학원", LocalTime.of(22, 0), null),
            areaName = "평촌 학원가",
            driveToPickupMin = 32,
            pickupToHomeMin = 18,
            homeName = "○○아파트",
            homeArrivalTime = LocalTime.of(22, 25),
            isMock = true,
        )
    }

    override suspend fun getPickupSpotChoice(): Result<PickupSpotChoice> = runCatching {
        val today = LocalDate.now(clock)
        PickupSpotChoice(
            candidates = listOf(
                PickupSpotCandidate(1, "△△빌딩 앞", SpotTag.RECOMMENDED, 5, "학원 앞 공사로 덜 붐벼요", 37.49460, 127.06200),
                PickupSpotCandidate(2, "○○ 앞", SpotTag.LAST_TIME, 3, "오늘 22시 붐빔", 37.49545, 127.06050),
                PickupSpotCandidate(3, "□□공원 입구", null, 6, "길 건너야 해요", 37.49395, 127.06085),
            ),
            basisText = "오늘 22시 학원가 상황을 보고 골랐어요",
            departureDt = today.atTime(21, 29),
            arrivalDt = today.atTime(22, 6),
            isMock = true,
        )
    }

    override suspend fun getDeparturePlan(): Result<DeparturePlan> = runCatching {
        val today = LocalDate.now(clock)
        DeparturePlan(
            departureDt = today.atTime(21, 29),
            arrivalDt = today.atTime(22, 4),
            originName = "회사",
            pickupName = "○○ 앞 픽업 장소",
            academyName = "수학학원",
            endTime = LocalTime.of(22, 0),
            walkMin = 3,
            // 시연 경로(DemoScenario.PICKUP)와 같은 좌표
            pickupLatitude = 37.49460,
            pickupLongitude = 127.06200,
            totalMin = 50,
            homeArrivalTime = LocalTime.of(22, 25),
            isMock = true,
            // 디자인 V08 예시: 학원 앞 단속 구역 → 걸어서 3분 거리로 안내, 22:04 배정
            enforcementRestricted = true,
        )
    }

    override suspend fun ensurePickupPlan(pickup: GeoPoint?): Result<AssignedPlan?> = runCatching {
        AssignedPlan(
            pickupPlanSeq = null,
            // 기본 지점 배정(좌표 없음)일 때만 이름이 있다. 부모가 고른 좌표면 CUSTOM(이름 없음) → 고른 후보 이름 유지
            pickupName = if (pickup == null) "○○ 앞 픽업 장소" else null,
            latitude = pickup?.lat ?: 37.49460,
            longitude = pickup?.lng ?: 127.06200,
            walkMin = 3,
            slotDt = LocalDate.now(clock).atTime(22, 4),
            guideText = "학원 정문 오른쪽 편의점 앞",
            isMock = true,
        )
    }

    override suspend fun getWaitingPlaces(): Result<WaitingPlaceChoice> = runCatching {
        val today = LocalDate.now(clock)
        WaitingPlaceChoice(
            departureDt = today.atTime(21, 29),
            places = listOf(
                WaitingPlace(1, "도곡초교 공영주차장", true, 5, "약 2,100원", 120, 37.49360, 127.05780, feeDetail = "기본 600원 · 10분 당 300원"),
                WaitingPlace(2, "대치2문화센터 공영", false, 5, "약 2,100원", 30, 37.49720, 127.06520),
                WaitingPlace(3, "도곡로421 공영", false, 4, "약 2,800원", 10, 37.49610, 127.05920),
            ),
            isMock = true,
        )
    }
}
