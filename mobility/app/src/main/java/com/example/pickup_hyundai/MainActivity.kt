package com.example.pickup_hyundai

import android.content.Intent
import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import com.example.pickup_hyundai.core.net.ApiClient
import com.example.pickup_hyundai.core.net.HttpCore
import com.example.pickup_hyundai.data.auth.AuthSession
import com.example.pickup_hyundai.data.remote.AuthApi
import com.example.pickup_hyundai.data.repository.AuthRepository
import com.example.pickup_hyundai.data.repository.RemoteAuthRepository
import com.example.pickup_hyundai.data.local.LocalRecordStore
import com.example.pickup_hyundai.data.local.SetupStore
import com.example.pickup_hyundai.data.remote.ChildApi
import com.example.pickup_hyundai.data.remote.ScheduleApi
import com.example.pickup_hyundai.data.remote.VehiclePickupApi
import com.example.pickup_hyundai.data.repository.LocalHomeRepository
import com.example.pickup_hyundai.data.repository.LocalSetupRepository
import com.example.pickup_hyundai.data.repository.MockAuthRepository
import com.example.pickup_hyundai.data.repository.MockDriveRepository
import com.example.pickup_hyundai.data.repository.MockHomeRepository
import com.example.pickup_hyundai.data.repository.MockMeetingRepository
import com.example.pickup_hyundai.data.repository.MockPickupRepository
import com.example.pickup_hyundai.data.repository.PickupRepository
import com.example.pickup_hyundai.data.repository.RemotePickupRepository
import com.example.pickup_hyundai.core.time.KstFormat
import com.example.pickup_hyundai.data.repository.TodayBase
import com.example.pickup_hyundai.domain.pickup.PickupSession
import com.example.pickup_hyundai.domain.route.GeoPoint
import java.time.LocalDate
import com.example.pickup_hyundai.notify.PickupNotifier
import com.example.pickup_hyundai.platform.gleo.GleoBridge
import com.example.pickup_hyundai.platform.gleo.GleoCaller
import com.example.pickup_hyundai.platform.gleo.GleoProtocol
import com.example.pickup_hyundai.platform.gleo.GleoRequest
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
 *   로그인 상태 시험 : ... --ez dev_logged_in true (debug 빌드만, Gleo 요청 허용 조건)
 *   Gleo 픽업 시작   : adb shell am start -a START_PICKUP -n com.example.pickup_hyundai/.MainActivity --es ACTION_PARAM '{"step":"TODAY"}' 
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
            PickupNotifier.show(this, "수학학원", LocalTime.of(22, 0), LocalDate.now().atTime(21, 29))
        }

        // 플랫폼(앱 생명주기는 WayMeRoot가 관리)
        val navi = NaviController(this)
        val location = VehicleLocationSource(this)
        val setupStore = SetupStore(this)
        if (BuildConfig.DEBUG && intent.getBooleanExtra(EXTRA_DEV_LOGGED_IN, false)) setupStore.loggedIn = true

        // Repository 주입 지점: local.properties RIDEMATE_API_BASE_URL이 있으면 실제 서버, 없으면 Mock
        val http = HttpCore(BuildConfig.RIDEMATE_API_BASE_URL)
        val remote = http.isConfigured
        val authSession = AuthSession(this, AuthApi(http), devAccessToken = BuildConfig.RIDEMATE_DEV_ACCESS_TOKEN)
        val authRepository: AuthRepository = if (remote) RemoteAuthRepository(AuthApi(http), authSession) else MockAuthRepository()
        // 로그인 상태: 서버 연동이면 실제 세션, Mock이면 차량 내 기록
        if (remote) setupStore.loggedIn = authSession.isLoggedIn
        // 첫 화면: 로그인 전 → QR 로그인, 로그인 후 초기 설정 전 → 아이 정보, 그 밖에는 홈 (개발용 start_screen 지정 시 제외)
        val entryScreen = when {
            intent.getStringExtra(EXTRA_START_SCREEN) != null -> null
            !setupStore.loggedIn -> Screen.LOGIN_QR
            setupStore.setupPending -> Screen.SETUP_CHILD
            else -> null
        }

        // 오늘 픽업 진행 상태(화면과 저장소가 함께 본다)
        val session = PickupSession()
        val childName = { setupStore.child?.name }
        // 서버 없이 쓰는 픽업 데이터는 초기 설정에서 등록한 오늘 시간표를 기준으로 계산한다
        val mockPickup = MockPickupRepository(today = {
            val today = LocalDate.now(KstFormat.ZONE)
            setupStore.schedules?.filter { it.pickupEnabled && it.day == today.dayOfWeek }?.minByOrNull { it.endTime }
                ?.let { TodayBase(childName()?.takeIf { n -> n.isNotBlank() } ?: "아이", it.academyName, it.endTime) }
        })
        // local.properties RIDEMATE_DEMO_PICKUP=true: 로그인은 실제 서버, 픽업 데이터는 차량에 등록한 시간표 기준(서버에 시간표가 없을 때 시연용)
        val pickupRepository: PickupRepository = if (remote && !mockEmpty && !BuildConfig.RIDEMATE_DEMO_PICKUP) {
            RemotePickupRepository(
                api = VehiclePickupApi(ApiClient(http, authSession)),
                fallback = mockPickup,
                currentLocation = { location.state.value.location?.let { GeoPoint(it.latitude, it.longitude) } },
                naviDriveMin = { navi.state.value.tripEtaSec?.let { (it + 59) / 60 } },
                childName = { setupStore.child?.name },
            )
        } else mockPickup
        val homeRepository = if (mockEmpty) MockHomeRepository(hasTodaySchedule = false)
        else LocalHomeRepository(setupStore, departure = { pickupRepository.getDeparturePlan().getOrNull()?.departureDt }) // 등록한 시간표만 표시
        val setupRepository = LocalSetupRepository(
            setupStore, naviHome = { navi.state.value.homeBookmark },
            scheduleApi = if (remote) ScheduleApi(ApiClient(http, authSession)) else null,
            childApi = if (remote) ChildApi(ApiClient(http, authSession)) else null,
        )
        val driveRepository = MockDriveRepository(session, childName)
        val recordStore = LocalRecordStore(this)
        val meetingRepository = MockMeetingRepository(intent.getStringExtra(EXTRA_MOCK_CHILD) ?: "LEAVING", session, childName)

        setContent {
            PickUp_HyundaiTheme {
                if (sdkCheck) SdkCheckHost() else WayMeRoot(
                    homeRepository, pickupRepository, meetingRepository, driveRepository, recordStore,
                    authRepository, setupRepository, setupStore, navi, location, startScreen, entryScreen,
                    session, onPickupReset = meetingRepository::reset,
                )
            }
        }
        handleGleoIntent(intent)
    }

    override fun onNewIntent(intent: Intent) {
        super.onNewIntent(intent)
        setIntent(intent)
        handleGleoIntent(intent)
    }

    /**
     * Gleo activity 함수(START_PICKUP). 액티비티 호출자는 referrer로만 짐작할 수 있고 위조 가능하므로 항상 '확인 안 됨'으로 취급한다.
     * 화면이 준비되면 WayMeAppContent의 처리기가 실행하고, 결과는 GleoBridge가 7초 안에 브로드캐스트로 돌려준다.
     */
    private fun handleGleoIntent(intent: Intent?) {
        val fn = intent?.action ?: return
        if (fn != GleoProtocol.FN_START_PICKUP) return
        val request = GleoRequest(fn, GleoProtocol.params(intent), GleoCaller(referrer?.host, verified = false))
        intent.action = Intent.ACTION_MAIN // 화면 회전 등으로 다시 만들어질 때 같은 요청을 반복하지 않도록
        GleoBridge.dispatch(this, request, waitForUi = true)
    }

    companion object {
        const val EXTRA_SDK_CHECK = "sdk_check"
        const val EXTRA_MOCK_EMPTY = "mock_empty"
        const val EXTRA_START_SCREEN = "start_screen"
        const val EXTRA_MOCK_CHILD = "mock_child"
        const val EXTRA_NOTIFY_TEST = "notify_test"
        const val EXTRA_DEV_LOGGED_IN = "dev_logged_in"
    }
}
