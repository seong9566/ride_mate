package com.example.pickup_hyundai.domain.model

import java.time.LocalDateTime
import java.time.LocalTime

/** V05 오늘의 픽업 출발 전 */
data class DeparturePlan(
    /** 정본: DetailDepartureGuide.departureDt */
    val departureDt: LocalDateTime,
    /** 정본: DetailDepartureGuide.childArrivalDt(픽업 장소 도착 예정) */
    val arrivalDt: LocalDateTime?,
    val originName: String,
    val pickupName: String,
    val academyName: String,
    val endTime: LocalTime,
    /** 정본: pickup.walkMin */
    val walkMin: Int?,
    val pickupLatitude: Double,
    val pickupLongitude: Double,
    /** 정본 API 없음(학원 근처 → 집 포함 총 소요) → Mock */
    val totalMin: Int?,
    val homeArrivalTime: LocalTime?,
    val isMock: Boolean,
    /** 정본 enforcement.status == RESTRICTED. null = UNKNOWN/미확인 */
    val enforcementRestricted: Boolean? = null,
    /** 정본 slotDt(오늘 계획이 있을 때만) */
    val slotDt: LocalDateTime? = null,
)

/** V06 대기 장소 후보. 정본 API 없음(백엔드 브랜치에 GetWaitingPlaceList 존재 → 정본 반영 시 교체) */
data class WaitingPlace(
    val rank: Int,
    val name: String,
    val recommended: Boolean,
    val driveMin: Int?,
    val feeText: String?,
    val availableSpaces: Int?,
    val latitude: Double,
    val longitude: Double,
    /** 요금 상세(예: "기본 600원 · 10분 당 300원"). 정본 없음 → Mock */
    val feeDetail: String? = null,
)

data class WaitingPlaceChoice(
    val departureDt: LocalDateTime,
    val places: List<WaitingPlace>,
    val isMock: Boolean,
)
