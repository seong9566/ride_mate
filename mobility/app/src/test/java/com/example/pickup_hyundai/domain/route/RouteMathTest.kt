package com.example.pickup_hyundai.domain.route

import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

class RouteMathTest {
    private val a = GeoPoint(37.4946, 127.0500)
    private val b = GeoPoint(37.4946, 127.0600)
    private val c = GeoPoint(37.4900, 127.0600)
    private val path = listOf(a, b, c)

    @Test fun atStart_remainingEqualsTotal() {
        val p = RouteMath.progress(path, a)
        assertEquals(p.totalM, p.remainingM, 1.0)
    }

    @Test fun atEnd_remainingZero() = assertEquals(0.0, RouteMath.progress(path, c).remainingM, 1.0)

    @Test fun midFirstSegment_isHalfOfFirstPlusSecond() {
        val mid = GeoPoint(37.4946, 127.0550)
        val p = RouteMath.progress(path, mid)
        val expected = RouteMath.distanceM(mid, b) + RouteMath.distanceM(b, c)
        assertEquals(expected, p.remainingM, 2.0)
    }

    @Test fun offRoutePoint_isSnappedOntoRoute() {
        val off = GeoPoint(37.4950, 127.0550) // 경로에서 약 44m 북쪽
        val p = RouteMath.progress(path, off)
        assertTrue(p.offRouteM in 30.0..60.0)
        assertEquals(37.4946, p.snapped.lat, 1e-6)
    }

    @Test fun estimate() = assertEquals(2, RouteMath.estimateMin(1000.0, 30.0))
}
