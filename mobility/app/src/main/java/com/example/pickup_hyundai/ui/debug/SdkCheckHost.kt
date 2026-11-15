package com.example.pickup_hyundai.ui.debug

import android.content.Context
import android.content.pm.PackageManager
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.Scaffold
import androidx.compose.runtime.Composable
import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.core.content.ContextCompat
import com.example.pickup_hyundai.platform.location.VehicleLocationSource
import com.example.pickup_hyundai.platform.navi.NaviController
import com.example.pickup_hyundai.platform.navi.NaviPlace
import com.example.pickup_hyundai.platform.vehicle.RearDoorMonitor

private const val PERMISSION_FUSED_LOCATION = "pleos.car.permission.FUSED_LOCATION"

// 검증용 좌표(API 명세서 예시값). 실제 좌표는 백엔드 응답으로 대체한다.
private val TEST_DESTINATION = NaviPlace("테스트 목적지(픽업 장소 예시)", 37.4991, 127.0302)
// 경유지는 목적지와 충분히 떨어진 좌표로 둔다(너무 가까우면 경로 탐색 오류 가능)
private val TEST_WAYPOINT = NaviPlace("테스트 경유지", 37.5045, 127.0490)

/** 1단계 SDK 검증 화면 호스트(개발용). Vehicle/Navi/Location 래퍼의 생명주기를 이 화면에 묶는다. */
@Composable
fun SdkCheckHost() {
    val context = LocalContext.current
    val door = remember { RearDoorMonitor(context) }
    val navi = remember { NaviController(context) }
    val location = remember { VehicleLocationSource(context) }

    DisposableEffect(Unit) {
        door.start()
        navi.start()
        onDispose {
            location.stop()
            navi.stop()
            door.stop()
        }
    }

    var granted by remember { mutableStateOf(context.hasPermission(PERMISSION_FUSED_LOCATION)) }
    val launcher = rememberLauncherForActivityResult(ActivityResultContracts.RequestPermission()) { granted = it }
    LaunchedEffect(granted) { if (granted) location.start() }

    val doorState by door.state.collectAsState()
    val naviState by navi.state.collectAsState()
    val locationState by location.state.collectAsState()

    Scaffold(modifier = Modifier.fillMaxSize()) { innerPadding ->
        SdkCheckScreen(
            door = doorState,
            navi = naviState,
            location = locationState,
            locationPermissionGranted = granted,
            onRequestLocationPermission = { launcher.launch(PERMISSION_FUSED_LOCATION) },
            onRequestRoute = { navi.requestRoute(TEST_DESTINATION) },
            onAddWaypoint = { navi.addWaypoint(TEST_WAYPOINT) },
            onRequestRouteWithWaypoint = { navi.requestRouteWithWaypoints(TEST_DESTINATION, listOf(TEST_WAYPOINT)) },
            onCancelRoute = { navi.cancelRoute() },
            onRequestTrip = { navi.requestTrip(TEST_DESTINATION) },
            modifier = Modifier.padding(innerPadding),
        )
    }
}

private fun Context.hasPermission(p: String) =
    ContextCompat.checkSelfPermission(this, p) == PackageManager.PERMISSION_GRANTED
