package com.example.pickup_hyundai.demo

import com.example.pickup_hyundai.core.time.KstFormat
import com.example.pickup_hyundai.domain.model.DriveProgress
import com.example.pickup_hyundai.domain.route.GeoPoint
import com.example.pickup_hyundai.domain.route.RouteMath
import com.example.pickup_hyundai.platform.location.LocationUiState
import com.example.pickup_hyundai.platform.navi.NaviUiState
import java.time.LocalTime

/** 화면에 표시하는 값이 어디서 왔는지(실제 데이터와 시연 데이터를 구분) */
enum class ProgressSource { NAVI /* Pleos NaviHelper */, ROUTE_ESTIMATE /* 위치+경로 계산 */, NONE }

data class LiveProgress(
    val progress: DriveProgress,
    val vehicle: GeoPoint?,
    val source: ProgressSource,
)

/**
 * 차량 위치(FusedLocation) + NaviHelper ETA + 경로를 합쳐 남은 시간·거리를 만든다.
 * 우선순위: NaviHelper(DrivingInfo: 남은 시간 초) → 경로 투영 추정. 거리는 경로 투영값을 사용
 * (DrivingInfo.distance는 문서상 "출발점부터의 거리"라 남은 거리로 확인되기 전까지 쓰지 않는다).
 */
object LiveDriveTracker {
    fun compute(
        location: LocationUiState,
        navi: NaviUiState,
        route: List<GeoPoint>,
        avgSpeedKmh: Double = 30.0,
        now: LocalTime = LocalTime.now(KstFormat.ZONE),
    ): LiveProgress {
        val loc = location.location?.let { GeoPoint(it.latitude, it.longitude) }
        val rp = loc?.let { RouteMath.progress(route, it) }
        val naviSec = navi.remainSec?.takeIf { navi.routing }
        val remainMin = when {
            naviSec != null -> (naviSec + 59) / 60
            rp != null -> RouteMath.estimateMin(rp.remainingM, avgSpeedKmh)
            else -> null
        }
        val source = when {
            naviSec != null -> ProgressSource.NAVI
            rp != null -> ProgressSource.ROUTE_ESTIMATE
            else -> ProgressSource.NONE
        }
        return LiveProgress(
            progress = DriveProgress(
                remainMin = remainMin,
                distanceKm = rp?.let { it.remainingM / 1000 },
                arrivalTime = remainMin?.let { now.plusMinutes(it.toLong()) },
                guidanceActive = navi.routing,
            ),
            vehicle = rp?.snapped ?: loc,
            source = source,
        )
    }
}
