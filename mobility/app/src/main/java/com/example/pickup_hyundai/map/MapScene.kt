package com.example.pickup_hyundai.map

import androidx.annotation.DrawableRes
import com.example.pickup_hyundai.domain.route.GeoPoint

/** 지도에 그릴 내용(지도 SDK와 무관한 순수 모델). 지도 제공자를 바꿔도 이 모델은 유지한다. */
data class MapMarker(
    val id: String,
    val position: GeoPoint,
    @DrawableRes val icon: Int,
    val text: String? = null,
)

data class MapPolygon(
    val id: String,
    /** 바깥 경계(첫 점 = 마지막 점이 되도록 렌더러가 닫는다) */
    val outer: List<GeoPoint>,
    val label: String? = null,
)

/** 디자인 "학원 근처" 원형 영역(DotPoints.fromCircle) */
data class MapCircle(val id: String, val center: GeoPoint, val radiusM: Float)

enum class RouteKind { VEHICLE /* 차량 경로 실선 */, WALK /* 아이 도보 */, PLANNED /* 예정 경로 */ }

data class MapRoute(
    val id: String,
    val points: List<GeoPoint>,
    val kind: RouteKind,
)

/** 지도 좌측 상단 요약 칩. 예: label "경로 미리보기", value "회사 → 픽업 장소 → 집" */
data class MapInfo(val label: String, val value: String)

data class MapScene(
    val markers: List<MapMarker> = emptyList(),
    val polygons: List<MapPolygon> = emptyList(),
    val circles: List<MapCircle> = emptyList(),
    val routes: List<MapRoute> = emptyList(),
    /** 카메라 중심. null이면 차량 위치 → 첫 마커 순 */
    val center: GeoPoint? = null,
    val zoom: Int = 15,
    /** 차량 마커 옆 문구. 예: "회사 · 현재 위치", "근처 정차 중" */
    val vehicleText: String? = null,
    /** 실제 차량 위치(FusedLocation)를 아직 모를 때 차량 마커를 둘 시연 위치. null이면 숨김 */
    val vehicleFallback: GeoPoint? = null,
    val info: MapInfo? = null,
    /** 하단 범례(차량 경로 / 아이 도보) 표시 */
    val showLegend: Boolean = false,
    /** 시연 경로·Mock 좌표가 섞였으면 true → 지도에 "시연 데이터" 표시 */
    val containsDemoData: Boolean = false,
)
