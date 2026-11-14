package com.example.pickup_hyundai

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import com.example.pickup_hyundai.core.net.ApiClient
import com.example.pickup_hyundai.core.net.ApiConfig
import com.example.pickup_hyundai.data.local.LocalRecordStore
import com.example.pickup_hyundai.data.local.SetupStore
import com.example.pickup_hyundai.data.remote.VehiclePickupApi
import com.example.pickup_hyundai.data.repository.LocalHomeRepository
import com.example.pickup_hyundai.data.repository.LocalSetupRepository
import com.example.pickup_hyundai.data.repository.MockAuthRepository
import com.example.pickup_hyundai.data.repository.MockDriveRepository
import com.example.pickup_hyundai.data.repository.MockHomeRepository
import com.example.pickup_hyundai.data.repository.MockMeetingRepository
import com.example.pickup_hyundai.data.repository.MockPickupRepository
import com.example.pickup_hyundai.data.repository.MockSetupRepository
import com.example.pickup_hyundai.data.repository.PickupRepository
import com.example.pickup_hyundai.data.repository.RemotePickupRepository
import com.example.pickup_hyundai.domain.route.GeoPoint
import com.example.pickup_hyundai.notify.PickupNotifier
import com.example.pickup_hyundai.platform.location.VehicleLocationSource
import com.example.pickup_hyundai.platform.navi.NaviController
import com.example.pickup_hyundai.ui.debug.SdkCheckHost
import com.example.pickup_hyundai.ui.navigation.Screen
import com.example.pickup_hyundai.ui.navigation.WayMeRoot
import com.example.pickup_hyundai.ui.theme.PickUp_HyundaiTheme
import java.time.LocalTime

/**
 * 진입점.
 * - 서버: local.properties에 RIDEMATE_API_BASE_URL·RIDEMATE_DEV_ACCESS_TOKEN(debug)이 있으면 정본 차량 API 사용, 없으면 Mock.
 * - 개발용 실행 옵션(adb):
 *   SDK 검증 화면   : adb shell am start -n com.example.pickup_hyundai/.MainActivity --ez sdk_check true
 *   일정 없음 Mock  : ... --ez mock_empty true
 *   화면 바로 열기  : ... --es start_screen PICKUP_GUIDE
 *                    (HOME, TODAY_PICKUP, PICKUP_SPOT, DEPARTURE, WAITING_PLACE, WAITING_DRIVE, WAITING_STAY, PICKUP_GUIDE,
 *                     SEND_LINK, PICKUP_STOP, HOME_DRIVE, RECORD,
 *                     LOGIN_QR, LOGIN_PHONE, LOGIN_OTP, LOGIN_CONFIRM, SETUP_CHILD, SETUP_TIMETABLE, SETUP_HOME, SETUP_DONE)
 *   아이 미확인 Mock : ... --es start_screen PICKUP_STOP --es mock_child NOT_OPENED
 *   시동 알림 시험   : ... --ez notify_test true   (debug 빌드만, 디자인 예시 문구)
 */
class MainActivity : ComponentActivity() {

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        enableEdgeToEdge()

        val sdkCheck = intent.getBooleanExtra(EXTRA_SDK_CHECK, false)
        val mockEmpty = intent.getBooleanExtra(EXTRA_MOCK_EMPTY, false)
        val startScreen = intent.getStringExtra(EXTRA_START_SCREEN)
            ?.let { name -> Screen.entries.firstOrNull { it.name == name } } ?: Screen.HOME

        if (BuildConfig.DEBUG && intent.getBooleanExtra(EXTRA_NOTIFY_TEST, false)) {
            // 디자인 V01 예시 값(시연용)
            PickupNotifier.show(this, "수학학원", LocalTime.of(22, 0), java.time.LocalDate.now().atTime(21, 29))
        }

        // 플랫폼(앱 생명주기는 WayMeRoot가 관리)
        val navi = NaviController(this)
        val location = VehicleLocationSource(this)
        val setupStore = SetupStore(this)

        // Repository 주입 지점
        val apiConfig = ApiConfig(BuildConfig.RIDEMATE_API_BASE_URL, BuildConfig.RIDEMATE_DEV_ACCESS_TOKEN)
        val mockPickup = MockPickupRepository()
        val pickupRepository: PickupRepository = if (apiConfig.isConfigured && !mockEmpty) {
            RemotePickupRepository(
                api = VehiclePickupApi(ApiClient(apiConfig)),
                fallback = mockPickup,
                currentLocation = { location.state.value.location?.let { GeoPoint(it.latitude, it.longitude) } },
                naviDriveMin = { navi.state.value.tripEtaSec?.let { (it + 59) / 60 } },
                childName = { setupStore.child?.name },
            )
        } else mockPickup
        val homeRepository = if (mockEmpty) MockHomeRepository(hasTodaySchedule = false)
        else LocalHomeRepository(setupStore, fallback = MockHomeRepository())
        val setupRepository = LocalSetupRepository(setupStore, MockSetupRepository(), naviHome = { navi.state.value.homeBookmark })
        val driveRepository = MockDriveRepository()
        val authRepository = MockAuthRepository()
        val recordStore = LocalRecordStore(this)
        val meetingRepository = MockMeetingRepository(childScenario = intent.getStringExtra(EXTRA_MOCK_CHILD) ?: "LEAVING")

        setContent {
            PickUp_HyundaiTheme {
                if (sdkCheck) SdkCheckHost() else WayMeRoot(
                    homeRepository, pickupRepository, meetingRepository, driveRepository, recordStore,
                    authRepository, setupRepository, setupStore, navi, location, startScreen,
                )
            }
        }
    }

    companion object {
        const val EXTRA_SDK_CHECK = "sdk_check"
        const val EXTRA_MOCK_EMPTY = "mock_empty"
        const val EXTRA_START_SCREEN = "start_screen"
        const val EXTRA_MOCK_CHILD = "mock_child"
        const val EXTRA_NOTIFY_TEST = "notify_test"
    }
}
