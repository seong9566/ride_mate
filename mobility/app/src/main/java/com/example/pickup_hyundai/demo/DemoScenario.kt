package com.example.pickup_hyundai.demo

import com.example.pickup_hyundai.domain.route.GeoPoint

/**
 * 시연 전용 경로(DEMO). gps_demo/daechi_scenario.json과 같은 좌표를 써야 한다.
 * 실제 서비스 데이터가 아니며, 화면에는 반드시 "시연 경로" 표시와 함께 사용한다.
 * 좌표는 근사값이므로 시연 전 지도에서 확인한다. 도로를 따르는 점은 `gps_demo.py build --osrm` 결과로 교체 가능.
 */
object DemoScenario {
    val START = GeoPoint(37.50455, 127.04905)      // 출발지 · 선릉역 인근
    val WAITING = GeoPoint(37.49360, 127.05780)    // 대기 장소(시연용)
    val PICKUP = GeoPoint(37.49460, 127.06200)     // 픽업 장소 · 학원가(시연용)
    val ACADEMY = GeoPoint(37.49500, 127.06120)    // 학원(시연용)
    val HOME = GeoPoint(37.48850, 127.05010)       // 집(시연용)

    val TO_WAITING = listOf(START, GeoPoint(37.49630, 127.05290), WAITING)
    val TO_PICKUP = listOf(WAITING, PICKUP)
    val TO_HOME = listOf(PICKUP, GeoPoint(37.49090, 127.05540), HOME)
}
