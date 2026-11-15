package com.example.pickup_hyundai.ui.navigation

import android.Manifest
import android.content.pm.PackageManager
import android.os.Build
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.runtime.Composable
import androidx.compose.runtime.CompositionLocalProvider
import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.platform.LocalContext
import androidx.core.content.ContextCompat
import com.example.pickup_hyundai.data.local.LocalRecordStore
import com.example.pickup_hyundai.data.local.SetupStore
import com.example.pickup_hyundai.data.repository.AuthRepository
import com.example.pickup_hyundai.data.repository.DriveRepository
import com.example.pickup_hyundai.data.repository.HomeRepository
import com.example.pickup_hyundai.data.repository.MeetingRepository
import com.example.pickup_hyundai.data.repository.PickupRepository
import com.example.pickup_hyundai.data.repository.SetupRepository
import com.example.pickup_hyundai.demo.LocalLocationState
import com.example.pickup_hyundai.demo.LocalNaviState
import com.example.pickup_hyundai.domain.route.GeoPoint
import com.example.pickup_hyundai.map.LocalVehicleLocation
import com.example.pickup_hyundai.domain.pickup.PickupSession
import com.example.pickup_hyundai.platform.location.VehicleLocationSource
import com.example.pickup_hyundai.platform.navi.LocalNaviController
import com.example.pickup_hyundai.platform.navi.NaviController

private const val PERMISSION_FUSED_LOCATION = "pleos.car.permission.FUSED_LOCATION"

/**
 * 앱 루트: Pleos 플랫폼(FusedLocation·NaviHelper)을 앱 생명주기에 묶고,
 * 차량 위치·내비 상태·내비 명령을 CompositionLocal로 모든 화면에 제공한다.
 * 런타임 권한: FUSED_LOCATION(위치), POST_NOTIFICATIONS(Android 13+, V01 시동 알림)
 */
@Composable
fun WayMeRoot(
    homeRepository: HomeRepository,
    pickupRepository: PickupRepository,
    meetingRepository: MeetingRepository,
    driveRepository: DriveRepository,
    recordStore: LocalRecordStore,
    authRepository: AuthRepository,
    setupRepository: SetupRepository,
    setupStore: SetupStore,
    navi: NaviController,
    location: VehicleLocationSource,
    startScreen: Screen = Screen.HOME,
    entryScreen: Screen? = null,
    session: PickupSession = PickupSession(),
    onPickupReset: () -> Unit = {},
) {
    val context = LocalContext.current
    fun has(p: String) = ContextCompat.checkSelfPermission(context, p) == PackageManager.PERMISSION_GRANTED

    var granted by remember { mutableStateOf(has(PERMISSION_FUSED_LOCATION)) }
    val launcher = rememberLauncherForActivityResult(ActivityResultContracts.RequestMultiplePermissions()) { result ->
        granted = result[PERMISSION_FUSED_LOCATION] ?: has(PERMISSION_FUSED_LOCATION)
        // 오늘의 픽업 알림은 홈 화면의 알림 카드로 보여 준다(시스템 알림은 시동(부팅) 때만)
    }
    LaunchedEffect(Unit) {
        val needed = buildList {
            if (!granted) add(PERMISSION_FUSED_LOCATION)
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU && !has(Manifest.permission.POST_NOTIFICATIONS)) {
                add(Manifest.permission.POST_NOTIFICATIONS)
            }
        }
        if (needed.isNotEmpty()) launcher.launch(needed.toTypedArray())
    }
    LaunchedEffect(granted) { if (granted) location.start() }

    DisposableEffect(Unit) {
        navi.start()
        onDispose {
            navi.stop()
            location.stop()
        }
    }

    val locState by location.state.collectAsState()
    val naviState by navi.state.collectAsState()
    val vehicle = locState.location?.let { GeoPoint(it.latitude, it.longitude) }

    CompositionLocalProvider(
        LocalVehicleLocation provides vehicle,
        LocalLocationState provides locState,
        LocalNaviState provides naviState,
        LocalNaviController provides navi,
    ) {
        WayMeAppContent(
            homeRepository, pickupRepository, meetingRepository, driveRepository, recordStore,
            authRepository, setupRepository, setupStore, startScreen, entryScreen, session, onPickupReset,
        )
    }
}
