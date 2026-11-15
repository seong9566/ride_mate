package com.example.pickup_hyundai.domain.model

import java.time.LocalDateTime
import java.time.LocalTime

/** V03 오늘 하원 일정 */
data class TodayPickupDetail(
    val item: PickupItem,
    /** 학원가 이름(예: 평촌 학원가) */
    val areaName: String?,
    /** 현재 위치 → 학원 근처(픽업) 차량 소요(분). 정본: DetailDepartureGuide.driveMin / NaviHelper TripInfo.eta */
    val driveToPickupMin: Int?,
    /** 학원 근처 → 집 차량 소요(분). 정본 API 없음 → 외부 경로 API 필요 */
    val pickupToHomeMin: Int?,
    val homeName: String,
    val homeArrivalTime: LocalTime?,
    val isMock: Boolean,
) {
    val totalMin: Int? get() = if (driveToPickupMin != null && pickupToHomeMin != null) driveToPickupMin + pickupToHomeMin else null
}

enum class SpotTag { RECOMMENDED, LAST_TIME }

/** V04 픽업 장소 후보 */
data class PickupSpotCandidate(
    val rank: Int,
    val name: String,
    val tag: SpotTag?,
    val walkMin: Int?,
    val reason: String?,
    val latitude: Double,
    val longitude: Double,
)

data class PickupSpotChoice(
    val candidates: List<PickupSpotCandidate>,
    /** 예: "오늘 22시 학원가 상황을 보고 골랐어요" */
    val basisText: String?,
    /** 추천 출발/도착. 정본: DetailDepartureGuide.departureDt / childArrivalDt */
    val departureDt: LocalDateTime?,
    val arrivalDt: LocalDateTime?,
    val isMock: Boolean,
)
