package com.example.pickup_hyundai.platform.navi

import ai.pleos.playground.navi.constants.BookmarkType
import ai.pleos.playground.navi.constants.NaviErrorCode
import ai.pleos.playground.navi.constants.NavigationStatus
import ai.pleos.playground.navi.constants.WaypointIndex
import ai.pleos.playground.navi.data.BookmarkInfo
import ai.pleos.playground.navi.data.DestinationArrivedInfo
import ai.pleos.playground.navi.data.DrivingInfo
import ai.pleos.playground.navi.data.RequestWaypointInfo
import ai.pleos.playground.navi.data.RouteInfo
import ai.pleos.playground.navi.data.RouteStartInfo
import ai.pleos.playground.navi.data.RouteWithWaypointsRequest
import ai.pleos.playground.navi.data.WaypointChangedInfo
import ai.pleos.playground.navi.data.WaypointInfo
import ai.pleos.playground.navi.data.TripInfo
import ai.pleos.playground.navi.data.TripRequest
import ai.pleos.playground.navi.data.WaypointArrivedInfo
import ai.pleos.playground.navi.helper.NaviHelper
import ai.pleos.playground.navi.helper.listener.NaviHelperEventListener
import android.annotation.SuppressLint
import android.content.Context
import android.content.Intent
import android.util.Log
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update

/** 앱 내부에서 쓰는 목적지 모델 (SDK 타입과 분리) */
data class NaviPlace(
    val name: String,
    val latitude: Double,
    val longitude: Double,
    val address: String = "",
)

data class NaviUiState(
    val connected: Boolean = false,
    /** 진단용: 단말에 Pleos Navi Service(ai.pleos.playground.service.navi)가 설치돼 있는지 */
    val naviServiceInstalled: Boolean? = null,
    /** initialize(onFailed) 사유 */
    val connectError: String? = null,
    val navigationStatus: NavigationStatus? = null,
    val routing: Boolean = false,
    /** DrivingInfo.destination.duration — 목적지까지 남은 시간(초) */
    val remainSec: Int? = null,
    /** DrivingInfo.destination.distance — SDK 문서상 "출발점부터의 거리(m)". 남은 거리인지 에뮬레이터 검증 필요 */
    val destinationDistanceM: Int? = null,
    /** TripInfo(requestTrip 응답): 거리(m)/ETA(초) */
    val tripDistanceM: Int? = null,
    val tripEtaSec: Int? = null,
    val arrived: DestinationArrivedInfo? = null,
    /** onWayPointInfo/onWaypointChanged로 확인된 경유지 이름 목록 */
    val waypoints: List<String> = emptyList(),
    /** 마지막으로 requestRoute한 목적지(도착 판정 대상) */
    val destination: NaviPlace? = null,
    /** 내비 '집' 즐겨찾기(getBookmarkInfo → BookmarkType.HOME). null = 없음/미조회 */
    val homeBookmark: NaviPlace? = null,
    val bookmarksLoaded: Boolean = false,
    val lastEvent: String? = null,
    val lastError: String? = null,
)

/**
 * Pleos NaviHelper SDK(3.0.5) 래퍼. 길안내는 기본 Pleos 내비게이션 앱이 수행하며,
 * 이 앱은 목적지·경유지 전달과 이벤트 수신만 한다(APP_MAPS 미선언, 단일 경로 정책 준수).
 *
 * 권한: NAVI_ROUTE_SEARCH(requestRoute·경유지·취소), NAVI_ROUTE(get*Info·requestTrip)
 * 근거: NaviHelper-3.0.5-sources.jar (NaviHelper.kt, NaviHelperEventListener.kt, data folders)
 */
@SuppressLint("MissingPermission")
class NaviController(context: Context) {

    private val appContext = context.applicationContext
    private val naviHelper = NaviHelper(appContext)
    private val _state = MutableStateFlow(NaviUiState())
    val state: StateFlow<NaviUiState> = _state.asStateFlow()
    private var started = false

    private val listener = object : NaviHelperEventListener {
        override fun onRouteStarted(routeStartInfo: RouteStartInfo) =
            event("onRouteStarted") { it.copy(routing = true, arrived = null) }

        override fun onRoutingCompleted() = event("onRoutingCompleted") { it.copy(routing = true) }

        override fun onRouteCancelled() =
            event("onRouteCancelled") { it.copy(routing = false, remainSec = null, destinationDistanceM = null) }

        override fun onReRouteEnded() = event("onReRouteEnded")

        override fun onDestinationChanged() = event("onDestinationChanged")

        override fun onDestinationArrived(destinationArrivedInfo: DestinationArrivedInfo) =
            event("onDestinationArrived") { it.copy(routing = false, arrived = destinationArrivedInfo) }

        override fun onWayPointArrived(waypointArrivedInfo: WaypointArrivedInfo) = event("onWayPointArrived")

        override fun onWaypointChanged(waypointChangedInfo: WaypointChangedInfo) {
            event("onWaypointChanged ${waypointChangedInfo.cmdType} ${waypointChangedInfo.index}")
            guarded("getWaypointInfo") { naviHelper.getWaypointInfo() }
        }

        override fun onWayPointInfo(waypointInfoList: List<WaypointInfo>) =
            event("onWayPointInfo(${waypointInfoList.size})") { s ->
                s.copy(waypoints = waypointInfoList.map { "${it.name}(남은 ${it.remainDistance}m·${it.remainTime}초)" })
            }

        override fun onDrivingInfo(drivingInfo: DrivingInfo) {
            _state.update {
                it.copy(
                    remainSec = drivingInfo.destination.duration,
                    destinationDistanceM = drivingInfo.destination.distance,
                )
            }
        }

        override fun onTripInfo(tripInfo: TripInfo) =
            event("onTripInfo") { it.copy(tripDistanceM = tripInfo.distance, tripEtaSec = tripInfo.eta) }

        override fun onNavigationStatus(status: NavigationStatus) =
            event("onNavigationStatus=$status") {
                it.copy(navigationStatus = status, routing = status == NavigationStatus.ROUTE_DRIVE)
            }

        override fun onBookmarkInfo(bookmarkInfoList: List<BookmarkInfo>) {
            val home = bookmarkInfoList.firstOrNull { it.category == BookmarkType.HOME }
            event("onBookmarkInfo(${bookmarkInfoList.size}, home=${home != null})") {
                it.copy(
                    bookmarksLoaded = true,
                    homeBookmark = home?.let { b -> NaviPlace(b.name.ifBlank { "집" }, b.latitude, b.longitude, b.address) },
                )
            }
        }

        override fun onError(errorCode: NaviErrorCode) =
            event("onError=$errorCode") { it.copy(lastError = errorCode.name) }
    }

    fun start() {
        if (started) return
        started = true
        _state.update { it.copy(naviServiceInstalled = isNaviServiceInstalled()) }
        naviHelper.addListener(listener)
        naviHelper.initialize(
            onConnected = {
                event("connected") { it.copy(connected = true) }
                requestBookmarks() // 초기 설정 '집' 주소에 사용
            },
            onFailed = { t -> event("connect failed") { it.copy(connected = false, connectError = t.message ?: t.javaClass.simpleName) } },
        )
    }

    fun stop() {
        if (!started) return
        started = false
        naviHelper.removeListener(listener)
        naviHelper.release()
    }

    /** 기본 내비에 목적지 전달. 성공 여부는 onRouteStarted/onRoutingCompleted/onError로 확인한다. */
    fun requestRoute(place: NaviPlace) {
        // 이전 목적지의 도착 이벤트가 새 목적지 도착으로 오인되지 않도록 초기화
        _state.update { it.copy(destination = place, arrived = null) }
        guarded("requestRoute") { naviHelper.requestRoute(place.toRouteInfo()) }
    }

    /** 내비 즐겨찾기 조회 → onBookmarkInfo. 권한 NAVI_ROUTE */
    fun requestBookmarks() = guarded("getBookmarkInfo") { naviHelper.getBookmarkInfo() }

    /**
     * 경유지 설정(최대 2개: FIRST, SECOND). 기존 안내 경로에 추가하는 API이므로 경로 안내 중에만 호출한다.
     * 경로가 없을 때는 [requestRouteWithWaypoints]를 사용한다.
     */
    fun addWaypoint(place: NaviPlace, index: WaypointIndex = WaypointIndex.FIRST) {
        if (!_state.value.routing) {
            _state.update { it.copy(lastError = "addWaypoint: 경로 안내 중이 아님 → 먼저 경로를 요청하세요") }
            return
        }
        addWaypointInternal(place, index)
    }

    /** 목적지 + 경유지를 한 번에 요청(경로가 없을 때). 결과는 onRouteStarted/onError로 확인. */
    fun requestRouteWithWaypoints(destination: NaviPlace, waypoints: List<NaviPlace>) =
        guarded("requestRouteWithWaypoints") {
            naviHelper.requestRouteWithWaypoints(
                RouteWithWaypointsRequest(
                    destination = destination.toRouteInfo(),
                    waypoints = waypoints.take(2).mapIndexed { i, p ->
                        p.toWaypointInfo(if (i == 0) WaypointIndex.FIRST else WaypointIndex.SECOND)
                    },
                )
            )
        }

    private fun addWaypointInternal(place: NaviPlace, index: WaypointIndex) = guarded("addWaypoint") {
        naviHelper.addWaypoint(place.toWaypointInfo(index))
    }

    fun removeAllWaypoints() = guarded("removeWaypoint") { naviHelper.removeWaypoint(WaypointIndex.ALL) }

    fun cancelRoute() = guarded("cancelRoute") { naviHelper.cancelRoute() }

    /** 경로 안내 없이 예상 거리/ETA 조회 → onTripInfo */
    fun requestTrip(place: NaviPlace) = guarded("requestTrip") {
        naviHelper.requestTrip(
            TripRequest(
                latitude = place.latitude,
                longitude = place.longitude,
                poiId = "",
                poiName = place.name,
                poiSubId = null,
            )
        )
    }

    private fun NaviPlace.toRouteInfo() = RouteInfo(
        latitude = latitude,
        longitude = longitude,
        poiName = name,
        poiId = "",
        address = address,
        poiSubId = "",
    )

    private fun NaviPlace.toWaypointInfo(index: WaypointIndex) = RequestWaypointInfo(
        latitude = latitude,
        longitude = longitude,
        poiName = name,
        waypointIndex = index,
        poiId = "",
        address = address,
        poiSubId = "",
    )

    private inline fun guarded(name: String, block: () -> Unit) {
        // 미연결 상태에서 호출하면 SDK가 요청을 대기열에 넣고 조용히 보류하므로, 화면에 명시한다.
        if (!_state.value.connected) {
            _state.update { it.copy(lastError = "$name: 내비 서비스 미연결(요청 보류)") }
        }
        runCatching(block).onFailure { e ->
            Log.e(TAG, "$name failed", e)
            _state.update { it.copy(lastError = "$name: ${e.message}") }
        }
    }

    private fun event(name: String, transform: (NaviUiState) -> NaviUiState = { it }) {
        Log.d(TAG, name)
        _state.update { transform(it).copy(lastEvent = name) }
    }

    /**
     * NaviHelper AAR이 manifest <queries>에 선언한 액션으로 바인딩 대상 서비스 존재 여부만 확인한다(진단용).
     * 패키지/액션 값은 NaviHelper-3.0.5-sources의 ServiceConstants 기준.
     */
    private fun isNaviServiceInstalled(): Boolean = runCatching {
        val intent = Intent(NAVI_ACTION).setPackage(NAVI_SERVICE_PACKAGE)
        appContext.packageManager.queryIntentServices(intent, 0).isNotEmpty()
    }.getOrDefault(false)

    private companion object {
        const val TAG = "WayMe/Navi"
        const val NAVI_SERVICE_PACKAGE = "ai.pleos.playground.service.navi"
        const val NAVI_ACTION = "ai.pleos.playground.intent.action.NAVI_START"
    }
}
