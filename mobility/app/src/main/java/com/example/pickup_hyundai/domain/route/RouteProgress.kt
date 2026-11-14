package com.example.pickup_hyundai.domain.route

import kotlin.math.asin
import kotlin.math.cos
import kotlin.math.roundToInt
import kotlin.math.sin
import kotlin.math.sqrt

data class GeoPoint(val lat: Double, val lng: Double)

/** 경로 진행 계산 결과 */
data class RouteProgress(
    /** 경로 위로 투영한 현재 위치(지도 마커 위치로 사용) */
    val snapped: GeoPoint,
    /** 경로에서 벗어난 거리(m). 크면 경로 이탈로 판단 */
    val offRouteM: Double,
    val remainingM: Double,
    val totalM: Double,
) {
    val fraction: Double get() = if (totalM <= 0) 1.0 else 1 - remainingM / totalM
}

/**
 * 현재 위치를 경로(폴리라인)에 투영해 남은 거리를 계산한다. 순수 Kotlin(SDK 의존 없음).
 * 짧은 구간(수 km)에서는 등장방형 근사로 충분하다.
 */
object RouteMath {
    private const val R = 6_371_000.0

    fun distanceM(a: GeoPoint, b: GeoPoint): Double {
        val la1 = Math.toRadians(a.lat); val la2 = Math.toRadians(b.lat)
        val dLa = la2 - la1; val dLo = Math.toRadians(b.lng - a.lng)
        val h = sin(dLa / 2) * sin(dLa / 2) + cos(la1) * cos(la2) * sin(dLo / 2) * sin(dLo / 2)
        return 2 * R * asin(sqrt(h))
    }

    fun lengthM(path: List<GeoPoint>): Double = path.zipWithNext { a, b -> distanceM(a, b) }.sum()

    fun progress(path: List<GeoPoint>, current: GeoPoint): RouteProgress {
        require(path.size >= 2) { "경로는 2개 이상의 점이 필요합니다" }
        val segLens = path.zipWithNext { a, b -> distanceM(a, b) }
        val total = segLens.sum()
        var best = Triple(0, 0.0, Double.MAX_VALUE) // (구간, t, 거리)
        var bestPoint = path.first()
        for (i in 0 until path.size - 1) {
            val (p, t) = project(path[i], path[i + 1], current)
            val d = distanceM(p, current)
            if (d < best.third) { best = Triple(i, t, d); bestPoint = p }
        }
        val (seg, t, off) = best
        val remaining = segLens[seg] * (1 - t) + segLens.drop(seg + 1).sum()
        return RouteProgress(bestPoint, off, remaining, total)
    }

    /** 선분 a-b 위로 c를 투영(위도 보정 평면 근사). 반환: (투영점, 0..1 비율) */
    private fun project(a: GeoPoint, b: GeoPoint, c: GeoPoint): Pair<GeoPoint, Double> {
        val k = cos(Math.toRadians((a.lat + b.lat) / 2))
        val ax = a.lng * k; val ay = a.lat
        val bx = b.lng * k; val by = b.lat
        val cx = c.lng * k; val cy = c.lat
        val dx = bx - ax; val dy = by - ay
        val len2 = dx * dx + dy * dy
        val t = if (len2 == 0.0) 0.0 else (((cx - ax) * dx + (cy - ay) * dy) / len2).coerceIn(0.0, 1.0)
        return GeoPoint(a.lat + (b.lat - a.lat) * t, a.lng + (b.lng - a.lng) * t) to t
    }

    /** 내비 ETA가 없을 때만 쓰는 추정치(분). 평균 속도 km/h 기준 */
    fun estimateMin(remainingM: Double, avgSpeedKmh: Double): Int =
        if (avgSpeedKmh <= 0) 0 else (remainingM / (avgSpeedKmh / 3.6) / 60).roundToInt()
}
