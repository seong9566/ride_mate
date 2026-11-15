package com.example.pickup_hyundai.demo

import com.example.pickup_hyundai.core.time.KstFormat
import com.example.pickup_hyundai.domain.model.DriveProgress
import com.example.pickup_hyundai.domain.route.GeoPoint
import com.example.pickup_hyundai.domain.route.RouteMath
import com.example.pickup_hyundai.platform.location.LocationUiState
import com.example.pickup_hyundai.platform.navi.NaviUiState
import java.time.LocalTime
import kotlin.math.ceil

internal const val NAVI_ETA_TIMEOUT_MS = 10_000L

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
        elapsedRealtimeMs: Long,
        avgSpeedKmh: Double = 30.0,
        now: LocalTime = LocalTime.now(KstFormat.ZONE),
    ): LiveProgress {
        val loc = location.location?.let { GeoPoint(it.latitude, it.longitude) }
        val target = route.last()
        val rp = loc?.takeIf { route.size > 1 }?.let { RouteMath.progress(route, it) }
        val remainingM = loc?.let {
            val directM = RouteMath.distanceM(it, target)
            // Use the distance to the destination when the vehicle has left the available path.
            maxOf(directM, rp?.takeIf { it.offRouteM <= 50.0 }?.remainingM ?: 0.0)
        }
        val guidanceActive = navi.routing && navi.destination?.let { GeoPoint(it.latitude, it.longitude) } == target
        val etaAgeMs = navi.remainChangedAtMs?.let { elapsedRealtimeMs - it }
        // Some navigation sessions keep reporting the same ETA while GPS continues moving.
        val naviSec = navi.remainSec?.takeIf {
            guidanceActive && it > 0 && (remainingM == null || (etaAgeMs != null && etaAgeMs in 0 until NAVI_ETA_TIMEOUT_MS))
        }
        val remainMin = when {
            naviSec != null -> (naviSec + 59) / 60
            remainingM != null -> ceil(remainingM / (avgSpeedKmh.coerceAtLeast(1.0) * 1000 / 60)).toInt()
            else -> null
        }
        val source = when {
            naviSec != null -> ProgressSource.NAVI
            remainingM != null -> ProgressSource.ROUTE_ESTIMATE
            else -> ProgressSource.NONE
        }
        return LiveProgress(
            progress = DriveProgress(
                remainMin = remainMin,
                distanceKm = remainingM?.div(1000),
                arrivalTime = remainMin?.let { now.plusMinutes(it.toLong()) },
                guidanceActive = guidanceActive,
            ),
            vehicle = rp?.snapped ?: loc,
            source = source,
        )
    }
}
