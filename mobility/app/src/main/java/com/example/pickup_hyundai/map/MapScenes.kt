package com.example.pickup_hyundai.map

import com.example.pickup_hyundai.R
import com.example.pickup_hyundai.demo.DemoScenario
import com.example.pickup_hyundai.domain.model.PickupSpotChoice
import com.example.pickup_hyundai.domain.model.WaitingPlace
import com.example.pickup_hyundai.domain.model.WaitingPlaceChoice
import com.example.pickup_hyundai.domain.route.GeoPoint

/**
 * 화면별 지도 장면 구성.
 * - 단속구역 "면"은 실제 도형 데이터(백엔드 또는 공공데이터)가 있을 때만 그린다(08_USER_FLOW §2.1).
 *   정본 DetailDepartureGuide.enforcement.status == RESTRICTED이면 학원 지점에 "단속 구역" 라벨만 단다(면을 만들지 않음).
 * - 도로를 따르는 차량 경로 좌표는 정본에 없어 시연 경로(DemoScenario)를 그린다(containsDemoData = true).
 */
object MapScenes {

    /** V02 홈: 경로 미리보기(회사 → 픽업 장소 → 집) */
    fun overview(
        pickupName: String = "○○ 앞 픽업 장소",
        academyName: String = "수학학원",
        pickup: GeoPoint = DemoScenario.PICKUP,
    ): MapScene = MapScene(
        markers = listOf(
            MapMarker("academy", DemoScenario.ACADEMY, R.drawable.map_academy, academyName),
            MapMarker("pickup", pickup, R.drawable.map_pin_pickup, pickupName),
            MapMarker("home", DemoScenario.HOME, R.drawable.map_pin_home, "집"),
        ),
        routes = listOf(
            MapRoute("to-pickup", listOf(DemoScenario.START, DemoScenario.WAITING, pickup), RouteKind.VEHICLE),
            MapRoute("to-home", listOf(pickup) + DemoScenario.TO_HOME.drop(1), RouteKind.PLANNED),
            MapRoute("walk", listOf(DemoScenario.ACADEMY, pickup), RouteKind.WALK),
        ),
        center = DemoScenario.WAITING, zoom = 14,
        vehicleText = "회사 · 현재 위치", vehicleFallback = DemoScenario.START,
        info = MapInfo("경로 미리보기", "회사 → 픽업 장소 → 집"),
        showLegend = true, containsDemoData = true,
    )

    /** V03 오늘 하원 일정: 학원 근처 원 + 집 */
    fun todayPickup(totalMin: Int?): MapScene = MapScene(
        markers = listOf(
            MapMarker("academy", DemoScenario.ACADEMY, R.drawable.map_academy, "학원 근처"),
            MapMarker("home", DemoScenario.HOME, R.drawable.map_pin_home, "집"),
        ),
        circles = listOf(MapCircle("near-academy", DemoScenario.ACADEMY, 300f)),
        routes = listOf(MapRoute("to-home", DemoScenario.TO_HOME, RouteKind.PLANNED)),
        center = DemoScenario.ACADEMY, zoom = 14,
        vehicleFallback = DemoScenario.START,
        info = totalMin?.let { MapInfo("집까지", "총 약 ${it}분") },
        containsDemoData = true,
    )

    /** V04 픽업 장소 후보(최대 3) + 학원 */
    fun pickupSpots(choice: PickupSpotChoice?, selectedRank: Int?, polygons: List<MapPolygon> = emptyList()): MapScene {
        val markers = buildList {
            add(MapMarker("academy", DemoScenario.ACADEMY, R.drawable.map_academy, "학원"))
            choice?.candidates?.take(3)?.forEach { c ->
                val on = c.rank == selectedRank
                add(MapMarker("spot-${c.rank}", GeoPoint(c.latitude, c.longitude), numIcon(c.rank, on), c.name))
            }
        }
        return MapScene(markers = markers, polygons = polygons, center = DemoScenario.ACADEMY, zoom = 16, containsDemoData = choice?.isMock != false)
    }

    /** V06 대기 장소 후보 + 학원·픽업 장소 + (차량 → 선택한 대기 장소) 경로 */
    fun waitingPlaces(choice: WaitingPlaceChoice?, selectedRank: Int?, pickupName: String = "○○ 앞 픽업 장소"): MapScene {
        val places = choice?.places?.take(3).orEmpty()
        val markers = buildList {
            add(MapMarker("academy", DemoScenario.ACADEMY, R.drawable.map_academy, "수학학원"))
            add(MapMarker("pickup", DemoScenario.PICKUP, R.drawable.map_pin_pickup, pickupName))
            places.forEach { p ->
                add(MapMarker("wait-${p.rank}", GeoPoint(p.latitude, p.longitude), if (p.rank == selectedRank) R.drawable.map_parking_on else R.drawable.map_parking_off, "${p.rank} ${p.name.removeSuffix(" 공영")}"))
            }
        }
        val selected = places.firstOrNull { it.rank == selectedRank }
        val maxMin = places.mapNotNull { it.driveMin }.maxOrNull()
        return MapScene(
            markers = markers,
            routes = listOfNotNull(
                selected?.let { MapRoute("to-wait", listOf(DemoScenario.START, GeoPoint(it.latitude, it.longitude)), RouteKind.VEHICLE) },
                MapRoute("walk", listOf(DemoScenario.ACADEMY, DemoScenario.PICKUP), RouteKind.WALK),
            ),
            center = DemoScenario.WAITING, zoom = 15,
            vehicleFallback = DemoScenario.START,
            info = if (places.isEmpty()) null else MapInfo("대기 후보", "${places.size}곳" + (maxMin?.let { " · 픽업 장소 ${it}분 이내" } ?: "")),
            containsDemoData = choice?.isMock != false,
        )
    }

    /** V07 대기 장소로 이동 */
    fun toWaiting(place: WaitingPlace?): MapScene = MapScene(
        markers = listOfNotNull(
            place?.let { MapMarker("dest", GeoPoint(it.latitude, it.longitude), R.drawable.map_parking_on, it.name) },
            MapMarker("pickup", DemoScenario.PICKUP, R.drawable.map_pin_pickup, "픽업 장소"),
        ),
        routes = listOf(MapRoute("route", DemoScenario.TO_WAITING, RouteKind.VEHICLE)),
        zoom = 15,
        containsDemoData = true,
    )

    /** V07A 대기 중: 대기 장소(현재 위치) → 픽업 장소 */
    fun waitingStay(place: WaitingPlace?, driveMin: Int?, pickupName: String): MapScene {
        val p = place?.let { GeoPoint(it.latitude, it.longitude) } ?: DemoScenario.WAITING
        return MapScene(
            markers = listOf(MapMarker("pickup", DemoScenario.PICKUP, R.drawable.map_pin_pickup, pickupName)),
            routes = listOf(MapRoute("next", listOf(p, DemoScenario.PICKUP), RouteKind.PLANNED)),
            center = p, zoom = 15,
            vehicleText = "${place?.name?.removeSuffix("주차장") ?: "대기 장소"} · 현재 위치",
            vehicleFallback = p,
            info = MapInfo("다음 이동", driveMin?.let { "픽업 장소까지 차로 ${it}분" } ?: "픽업 장소까지"),
            containsDemoData = true,
        )
    }

    /** V08 픽업 장소 안내: 배정 장소 + 아이 도보 + (단속 구역이면) 학원 라벨 */
    fun pickupGuide(pickupName: String, walkMin: Int?, restricted: Boolean, infoValue: String?): MapScene = MapScene(
        markers = listOf(
            MapMarker("academy", DemoScenario.ACADEMY, R.drawable.map_academy, if (restricted) "단속 구역" else "학원"),
            MapMarker("pickup", DemoScenario.PICKUP, R.drawable.map_pin_pickup, pickupName + (walkMin?.let { "  · 아이 도보 ${it}분" } ?: "")),
        ),
        routes = listOf(
            MapRoute("to-pickup", DemoScenario.TO_PICKUP, RouteKind.VEHICLE),
            MapRoute("walk", listOf(DemoScenario.ACADEMY, DemoScenario.PICKUP), RouteKind.WALK),
        ),
        zoom = 16,
        vehicleFallback = DemoScenario.WAITING,
        info = infoValue?.let { MapInfo("도착", it) },
        showLegend = true, containsDemoData = true,
    )

    /** V09 만남 연결(픽업 장소 보내기) */
    fun sendLink(walkMin: Int?, remainMin: Int?): MapScene = MapScene(
        markers = listOf(
            MapMarker("academy", DemoScenario.ACADEMY, R.drawable.map_academy, "학원"),
            MapMarker("pickup", DemoScenario.PICKUP, R.drawable.map_pin_pickup, "픽업 장소"),
        ),
        routes = listOf(
            MapRoute("to-pickup", DemoScenario.TO_PICKUP, RouteKind.VEHICLE),
            MapRoute("walk", listOf(DemoScenario.ACADEMY, DemoScenario.PICKUP), RouteKind.WALK),
        ),
        center = DemoScenario.PICKUP, zoom = 16,
        vehicleText = remainMin?.let { "남은 ${it}분" },
        vehicleFallback = DemoScenario.WAITING,
        info = MapInfo("보내기 좋은 시점", walkMin?.let { "아이 도보 ${it}분 + 1분 전" } ?: "도착 1분 전"),
        containsDemoData = true,
    )

    /** V09 픽업 정차 */
    fun pickupStop(): MapScene = MapScene(
        markers = listOf(
            MapMarker("academy", DemoScenario.ACADEMY, R.drawable.map_academy, "학원"),
            MapMarker("pickup", DemoScenario.PICKUP, R.drawable.map_pin_pickup, "배정된 픽업 장소"),
        ),
        routes = listOf(MapRoute("walk", listOf(DemoScenario.ACADEMY, DemoScenario.PICKUP), RouteKind.WALK)),
        center = DemoScenario.PICKUP, zoom = 17,
        vehicleText = "근처 정차 중", vehicleFallback = DemoScenario.PICKUP,
        containsDemoData = true,
    )

    /** V10 집으로 가는 중 */
    fun toHome(): MapScene = MapScene(
        markers = listOf(MapMarker("home", DemoScenario.HOME, R.drawable.map_pin_home, "집")),
        routes = listOf(MapRoute("route", DemoScenario.TO_HOME, RouteKind.VEHICLE)),
        zoom = 15, containsDemoData = true,
    )

    private fun numIcon(rank: Int, on: Boolean) = when (rank) {
        1 -> if (on) R.drawable.map_num_1_on else R.drawable.map_num_1_off
        2 -> if (on) R.drawable.map_num_2_on else R.drawable.map_num_2_off
        else -> if (on) R.drawable.map_num_3_on else R.drawable.map_num_3_off
    }
}
