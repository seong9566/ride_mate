package com.example.pickup_hyundai.domain.model

import java.time.LocalDateTime
import java.time.LocalTime

/**
 * 주행 안내 중 표시 정보. 실제 값은 5단계에서 NaviHelper(DrivingInfo.destination.duration/distance,
 * onNavigationStatus)로 채운다. 현재는 Mock.
 */
data class DriveProgress(
    val remainMin: Int?,
    val distanceKm: Double?,
    val arrivalTime: LocalTime?,
    /** 기본 내비가 경로 안내 중인지(NavigationStatus.ROUTE_DRIVE) */
    val guidanceActive: Boolean,
)

/** V07 대기 장소로 이동 */
data class WaitingDrive(
    val place: WaitingPlace,
    val progress: DriveProgress,
    val isMock: Boolean,
)

/** V10 집으로 가는 중 */
data class HomeDrive(
    val homeName: String,
    val childName: String,
    val boardedAt: LocalDateTime?,
    val boardedPlaceName: String?,
    val progress: DriveProgress,
    val isMock: Boolean,
)

/** V11 픽업 완료 요약(기록 저장 대상) */
data class PickupRecord(
    val date: java.time.LocalDate,
    val departedAt: LocalTime?,
    val departurePlaceName: String?,
    val boardedAt: LocalTime?,
    val boardedPlaceName: String?,
    val homeArrivedAt: LocalTime?,
    val totalMin: Int?,
    val waitMin: Int?,
    val loopCount: Int,
)
