package com.example.pickup_hyundai

import com.example.pickup_hyundai.demo.DemoScenario
import com.example.pickup_hyundai.demo.LiveDriveTracker
import com.example.pickup_hyundai.demo.ProgressSource
import com.example.pickup_hyundai.domain.route.GeoPoint
import com.example.pickup_hyundai.platform.location.LocationUiState
import com.example.pickup_hyundai.platform.location.VehicleLocation
import com.example.pickup_hyundai.platform.navi.NaviUiState
import com.example.pickup_hyundai.platform.navi.NaviPlace
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

class LiveDriveTrackerTest {
    @Test
    fun passingTheRegisteredHomeOffRouteDoesNotShowZeroMinutes() {
        val home = GeoPoint(37.4900, 127.0520)
        val route = listOf(DemoScenario.PICKUP, home)

        val offRoute = progressAt(DemoScenario.HOME, route)
        assertTrue(requireNotNull(offRoute.remainMin) > 0)
        assertTrue(requireNotNull(offRoute.distanceKm) > 0.05)

        val atHome = progressAt(home, route)
        assertEquals(0, atHome.remainMin)
    }

    @Test
    fun gpsReducesRemainingTimeOnAllThreeLegs() {
        for (route in listOf(DemoScenario.TO_WAITING, DemoScenario.TO_PICKUP, DemoScenario.TO_HOME)) {
            val start = route.first()
            val target = route.last()
            val near = GeoPoint(target.lat + (start.lat - target.lat) * 0.1, target.lng + (start.lng - target.lng) * 0.1)
            val startProgress = progressAt(start, route)
            val nearProgress = progressAt(near, route)
            assertTrue(requireNotNull(startProgress.distanceKm) > requireNotNull(nearProgress.distanceKm))
            assertTrue(requireNotNull(startProgress.remainMin) >= requireNotNull(nearProgress.remainMin))
            assertEquals(0, progressAt(target, route).remainMin)
        }
    }

    @Test
    fun staleOrDifferentDestinationEtaFallsBackToGps() {
        val target = DemoScenario.WAITING
        val route = listOf(target)
        val start = DemoScenario.START
        val gps = LocationUiState(location = VehicleLocation(start.lat, start.lng, 0f, 0L))
        val navi = NaviUiState(
            routing = true,
            destination = NaviPlace("대기 장소", target.lat, target.lng),
            remainSec = 600,
            remainChangedAtMs = 1_000,
        )
        val fresh = LiveDriveTracker.compute(gps, navi, route, elapsedRealtimeMs = 1_500)
        assertEquals(10, fresh.progress.remainMin)
        assertEquals(ProgressSource.NAVI, fresh.source)

        val stale = LiveDriveTracker.compute(gps, navi, route, elapsedRealtimeMs = 11_000)
        val wrongDestination = LiveDriveTracker.compute(
            gps, navi.copy(destination = NaviPlace("이전 목적지", start.lat, start.lng)), route, elapsedRealtimeMs = 1_500,
        )
        assertEquals(ProgressSource.ROUTE_ESTIMATE, stale.source)
        assertEquals(ProgressSource.ROUTE_ESTIMATE, wrongDestination.source)
        assertEquals(stale.progress.remainMin, wrongDestination.progress.remainMin)
    }

    private fun progressAt(point: GeoPoint, route: List<GeoPoint>) = LiveDriveTracker.compute(
        LocationUiState(location = VehicleLocation(point.lat, point.lng, 0f, 0L)),
        NaviUiState(),
        route,
        elapsedRealtimeMs = 0,
    ).progress
}
