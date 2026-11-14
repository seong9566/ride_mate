package com.example.pickup_hyundai.ui.navigation

import org.json.JSONObject
import org.json.JSONArray
import com.example.pickup_hyundai.platform.gleo.GleoStatus
import com.example.pickup_hyundai.platform.gleo.GleoRequest
import kotlinx.coroutines.withTimeoutOrNull
import com.example.pickup_hyundai.platform.gleo.GleoResult
import com.example.pickup_hyundai.platform.gleo.GleoProtocol
import com.example.pickup_hyundai.platform.gleo.GleoBridge
import com.example.pickup_hyundai.platform.gleo.GleoActionHandler
import android.widget.Toast
import androidx.activity.compose.BackHandler
import androidx.compose.runtime.Composable
import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.State
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateListOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.platform.LocalContext
import androidx.lifecycle.viewmodel.compose.viewModel
import ai.pleos.playground.vehicle.constant.control.VehicleGear
import com.example.pickup_hyundai.core.time.KstFormat
import com.example.pickup_hyundai.data.local.LocalRecordStore
import com.example.pickup_hyundai.data.local.SetupStore
import com.example.pickup_hyundai.data.repository.AuthRepository
import com.example.pickup_hyundai.data.repository.DriveRepository
import com.example.pickup_hyundai.data.repository.HomeRepository
import com.example.pickup_hyundai.data.repository.MeetingRepository
import com.example.pickup_hyundai.data.repository.PickupRepository
import com.example.pickup_hyundai.data.repository.SetupRepository
import com.example.pickup_hyundai.demo.DemoScenario
import com.example.pickup_hyundai.demo.LiveDriveTracker
import com.example.pickup_hyundai.demo.LocalLocationState
import com.example.pickup_hyundai.demo.LocalNaviState
import com.example.pickup_hyundai.demo.withLiveProgress
import com.example.pickup_hyundai.domain.PickupFlowRules
import com.example.pickup_hyundai.domain.model.WaitingPlace
import com.example.pickup_hyundai.domain.pickup.PickupSession
import com.example.pickup_hyundai.domain.route.GeoPoint
import com.example.pickup_hyundai.domain.route.RouteMath
import com.example.pickup_hyundai.map.LocalVehicleLocation
import com.example.pickup_hyundai.platform.navi.LocalNaviController
import com.example.pickup_hyundai.platform.navi.NaviPlace
import com.example.pickup_hyundai.platform.vehicle.GearMonitor
import com.example.pickup_hyundai.platform.vehicle.RearDoorMonitor
import com.example.pickup_hyundai.platform.vehicle.isParked
import com.example.pickup_hyundai.ui.auth.AuthEvent
import com.example.pickup_hyundai.ui.auth.AuthViewModel
import com.example.pickup_hyundai.ui.auth.LoginConfirmScreen
import com.example.pickup_hyundai.ui.auth.OtpScreen
import com.example.pickup_hyundai.ui.auth.PhoneLoginScreen
import com.example.pickup_hyundai.ui.auth.QrLoginScreen
import com.example.pickup_hyundai.ui.home.HomeScreen
import com.example.pickup_hyundai.ui.home.HomeViewModel
import com.example.pickup_hyundai.ui.pickup.DepartureScreen
import com.example.pickup_hyundai.ui.pickup.DepartureViewModel
import com.example.pickup_hyundai.ui.pickup.DriveViewModelFactory
import com.example.pickup_hyundai.ui.pickup.HomeDriveScreen
import com.example.pickup_hyundai.ui.pickup.HomeDriveViewModel
import com.example.pickup_hyundai.ui.pickup.LoadState
import com.example.pickup_hyundai.ui.pickup.MeetingViewModelFactory
import com.example.pickup_hyundai.ui.pickup.PickupGuideInfo
import com.example.pickup_hyundai.ui.pickup.PickupGuideScreen
import com.example.pickup_hyundai.ui.pickup.PickupSpotScreen
import com.example.pickup_hyundai.ui.pickup.PickupSpotViewModel
import com.example.pickup_hyundai.ui.pickup.PickupStopScreen
import com.example.pickup_hyundai.ui.pickup.PickupStopViewModel
import com.example.pickup_hyundai.ui.pickup.PickupViewModelFactory
import com.example.pickup_hyundai.ui.pickup.RecordScreen
import com.example.pickup_hyundai.ui.pickup.RecordViewModel
import com.example.pickup_hyundai.ui.pickup.SendLinkScreen
import com.example.pickup_hyundai.ui.pickup.SendLinkViewModel
import com.example.pickup_hyundai.ui.pickup.TodayPickupScreen
import com.example.pickup_hyundai.ui.pickup.TodayPickupViewModel
import com.example.pickup_hyundai.ui.pickup.WaitingDriveScreen
import com.example.pickup_hyundai.ui.pickup.WaitingDriveViewModel
import com.example.pickup_hyundai.ui.pickup.WaitingPlaceScreen
import com.example.pickup_hyundai.ui.pickup.WaitingPlaceViewModel
import com.example.pickup_hyundai.ui.pickup.WaitingStayInfo
import com.example.pickup_hyundai.ui.pickup.WaitingStayScreen
import com.example.pickup_hyundai.ui.setup.ChildInfoScreen
import com.example.pickup_hyundai.ui.setup.HomeSetupScreen
import com.example.pickup_hyundai.ui.setup.SetupDoneScreen
import com.example.pickup_hyundai.ui.setup.SetupEvent
import com.example.pickup_hyundai.ui.setup.SetupViewModel
import com.example.pickup_hyundai.ui.setup.TimetableScreen
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch
import java.time.Duration
import java.time.LocalDateTime

/** 08_USER_FLOW 화면 ID */
enum class Screen {
    HOME /* V02 */, TODAY_PICKUP /* V03 */, PICKUP_SPOT /* V04 */, DEPARTURE /* V05 */, WAITING_PLACE /* V06 */,
    WAITING_DRIVE /* V07 */, WAITING_STAY /* V07A 대기 중 */, PICKUP_GUIDE /* V08 픽업 장소 안내 */,
    SEND_LINK /* V09 만남 연결 */, PICKUP_STOP /* V09 정차 */, HOME_DRIVE /* V10 */, RECORD /* V11 */,
    LOGIN_QR /* A00 */, LOGIN_PHONE, LOGIN_OTP, LOGIN_CONFIRM /* 휴대폰 확인 */, SETUP_CHILD /* A01 */, SETUP_TIMETABLE, SETUP_HOME, SETUP_DONE,
}

/** 픽업 진행 단계 화면(Gleo 요청으로 단계를 옮길 때 뒤로 가기 대상이 되지 않도록 교체) */
private val FLOW_SCREENS = setOf(
    Screen.DEPARTURE, Screen.WAITING_PLACE, Screen.WAITING_DRIVE, Screen.WAITING_STAY,
    Screen.PICKUP_GUIDE, Screen.SEND_LINK, Screen.PICKUP_STOP, Screen.HOME_DRIVE,
)
/** 아이를 태운 뒤 단계 */
private val AFTER_PICKUP = setOf(Screen.HOME_DRIVE, Screen.RECORD)

/** 운전자 확인이 필요한 Gleo 요청 */
private class GleoConfirm(val label: String, val onConfirm: () -> Unit)

private val BEFORE_DEPARTURE = setOf(Screen.TODAY_PICKUP, Screen.PICKUP_SPOT, Screen.DEPARTURE, Screen.WAITING_PLACE)

/** Gleo 상태 안내 첫 문장(영어: Gleo Interface 규칙) */
private val STAGE_TEXT = mapOf(
    Screen.TODAY_PICKUP to "Today's pickup has not started yet.",
    Screen.PICKUP_SPOT to "The pickup spot is being chosen.",
    Screen.DEPARTURE to "The car has not left yet.",
    Screen.WAITING_PLACE to "A waiting place is being chosen.",
    Screen.WAITING_DRIVE to "The car is on the way to the waiting place.",
    Screen.WAITING_STAY to "The car is waiting at the waiting place.",
    Screen.PICKUP_GUIDE to "The car is on the way to the pickup spot.",
    Screen.SEND_LINK to "The car is about to send the pickup spot to the child.",
    Screen.PICKUP_STOP to "The car is stopped at the pickup spot and waiting for the child.",
    Screen.HOME_DRIVE to "The child is in the car and on the way home.",
    Screen.RECORD to "Today's pickup is complete.",
)

/** 도착 판정 반경(m). NaviHelper onDestinationArrived가 오지 않는 경우(에뮬레이터 등) FusedLocation으로 보조 판정 */
private const val ARRIVAL_RADIUS_M = 50.0

/**
 * 앱 화면 전환(경량 백스택) + 픽업 진행 로직.
 *
 * 픽업 흐름(PickupSession 공유):
 *  V03 → V04(장소 확정 → AddPickupPlan) → 출발까지 30분 이상이면 V06 대기 장소, 아니면 V05 출발 전
 *  V06 경로 시작 → requestRoute(대기 장소) → V07 → 도착 → V07A 대기 중 → "지금 출발" → requestRoute(픽업 장소) → V08
 *  V05 경로 시작(P단) → requestRoute(픽업 장소) → V08
 *  V08: 남은 시간 ≤ 아이 도보 + 1분이면 V09 만남 연결(보내기/나중에), 픽업 장소 도착 → 정차
 *  정차: 뒷문 열림→닫힘(탑승 후보) → 운전자 확인 → requestRoute(집) → V10 → 도착 → V11 기록
 */
@Composable
internal fun WayMeAppContent(
    homeRepository: HomeRepository,
    pickupRepository: PickupRepository,
    meetingRepository: MeetingRepository,
    driveRepository: DriveRepository,
    recordStore: LocalRecordStore,
    authRepository: AuthRepository,
    setupRepository: SetupRepository,
    setupStore: SetupStore,
    /** 개발용: 특정 화면부터 시작(adb --es start_screen DEPARTURE 등) */
    startScreen: Screen = Screen.HOME,
    /** 서버 연동 중이고 이 차에 로그인한 계정이 없으면 QR 로그인부터(뒤로 가기로 우회 불가) */
    loginRequired: Boolean = false,
) {
    val context = LocalContext.current
    val navi = LocalNaviController.current
    val naviState = LocalNaviState.current
    val scope = rememberCoroutineScope()
    val session = remember { PickupSession() }
    var gleoConfirm by remember { mutableStateOf<GleoConfirm?>(null) }

    val backStack = remember {
        when {
            loginRequired -> mutableStateListOf(Screen.LOGIN_QR)
            startScreen == Screen.HOME -> mutableStateListOf(Screen.HOME)
            else -> mutableStateListOf(Screen.HOME, startScreen)
        }
    }
    val current = backStack.last()
    fun push(s: Screen) { backStack.add(s) }
    fun pop() { if (backStack.size > 1) backStack.removeAt(backStack.lastIndex) }
    /** 주행 단계 전환: 이전 단계로 뒤로 가기 되지 않도록 맨 위 화면을 바꾼다 */
    fun replaceTop(s: Screen) { backStack[backStack.lastIndex] = s }
    fun resetToHome() { while (backStack.size > 1) backStack.removeAt(backStack.lastIndex) }
    /** 로그인·초기 설정 완료 후 뒤로 가기로 되돌아가지 않도록 스택을 교체 */
    fun replaceAll(s: Screen) { backStack.clear(); backStack.add(Screen.HOME); if (s != Screen.HOME) backStack.add(s) }
    fun toast(msg: String) = Toast.makeText(context, msg, Toast.LENGTH_SHORT).show()
    fun notReady(name: String) = toast("$name 은(는) 다음 단계에서 연결됩니다")
    fun now(): LocalDateTime = LocalDateTime.now(KstFormat.ZONE)

    /** 기본 내비에 목적지 전달(길안내는 Pleos 내비가 수행). 내비 미연결이어도 화면은 진행하고 상태를 알린다 */
    fun routeTo(name: String, p: GeoPoint, address: String = "") {
        if (navi == null) { toast("내비 연결 전이에요"); return }
        navi.requestRoute(NaviPlace(name, p.lat, p.lng, address))
        if (!naviState.connected) toast("내비 연결 후 경로가 시작돼요")
    }
    val pickupPoint = session.pickupPoint ?: DemoScenario.PICKUP
    val pickupName = session.pickupName ?: "픽업 장소"
    fun homePlace(): NaviPlace = setupStore.home?.let { NaviPlace("집", it.latitude, it.longitude, it.address) }
        ?: naviState.homeBookmark
        ?: NaviPlace("집(시연)", DemoScenario.HOME.lat, DemoScenario.HOME.lng)

    BackHandler(enabled = backStack.size > 1) { pop() }

    // ── Gleo(현대 차량 AI) 요청 처리 ──
    // Gleo가 말을 이해해 schema.json 함수를 부르면 여기서 실행하고 영어 문장으로 결과를 돌려준다.
    // 로그인·중복 확인은 GleoBridge가 먼저 하고, 여기서는 "지금 단계에서 가능한 요청인지"를 확인한다.
    /** 진행 단계 화면 사이 이동: 픽업 진행 중이면 맨 위 화면을 바꾸고, 홈 등에서는 새로 연다 */
    fun goTo(s: Screen) {
        val top = backStack.last()
        if (top == s) return
        if (top in FLOW_SCREENS) replaceTop(s) else push(s)
    }
    fun fail(fn: String, msg: String) = GleoResult(fn, GleoStatus.FAIL, msg)

    suspend fun handleGleo(req: GleoRequest): GleoResult? {
        val fn = req.functionName
        val screen = backStack.last()
        val started = screen in FLOW_SCREENS || screen in BEFORE_DEPARTURE
        val pName = session.pickupName ?: "the pickup spot"
        return when (fn) {
            // 1) 일반 정보 조회: 로그인 확인 후 응답, 개인정보 최소화(아이 이름·주소·좌표 미포함)
            GleoProtocol.FN_GET_PICKUP_STATUS -> {
                if (!started && session.departureDt == null) return null // 진행 중인 픽업 없음 → 저장된 일정 안내
                val nav = navi?.state?.value
                val remainMin = nav?.remainSec?.takeIf { nav.routing }?.let { (it + 59) / 60 }
                val minsToDeparture = session.departureDt?.let { Duration.between(now(), it).toMinutes() }
                val msg = buildString {
                    append(STAGE_TEXT[screen] ?: "Today's pickup is being prepared.")
                    session.endTime?.let { append(" ${session.academyName ?: "The academy"} ends at ${KstFormat.hm(it)}.") }
                    if (screen in BEFORE_DEPARTURE && minsToDeparture != null) {
                        append(
                            if (minsToDeparture > 0) " The recommended departure is ${KstFormat.hm(session.departureDt!!.toLocalTime())}, in $minsToDeparture minutes."
                            else " It is time to leave now."
                        )
                    }
                    remainMin?.let { append(" About $it minutes remain to the destination.") }
                    session.pickupName?.let { append(" The pickup spot is $it.") }
                    session.plan?.slotDt?.let { append(" The assigned arrival time is ${KstFormat.hm(it.toLocalTime())}.") }
                }
                val data = JSONArray().put(JSONObject().apply {
                    put("stage", screen.name)
                    session.academyName?.let { put("academyName", it) }
                    session.endTime?.let { put("endTime", KstFormat.hm(it)) }
                    session.departureDt?.let { put("departureTime", KstFormat.hm(it.toLocalTime())) }
                    session.pickupName?.let { put("pickupSpot", it) }
                    remainMin?.let { put("remainingMinutes", it) }
                })
                GleoResult(fn, GleoStatus.SUCCESS, msg, data)
            }

            // 2) 상태 변경: 현재 단계에서 유효한 요청만 실행(화면에 결과가 보임)
            GleoProtocol.FN_START_PICKUP -> when (req.params.optString("step")) {
                "TODAY" -> {
                    if (screen in FLOW_SCREENS && session.departedAt != null) {
                        return fail(fn, "Today's pickup is already in progress.")
                    }
                    val today = now().toLocalDate()
                    val first = setupStore.schedules?.filter { it.pickupEnabled && it.day == today.dayOfWeek }?.minByOrNull { it.endTime }
                    if (setupStore.schedules != null && first == null) return fail(fn, "There is no pickup scheduled for today.")
                    session.reset()
                    replaceAll(Screen.TODAY_PICKUP)
                    GleoResult(
                        fn, GleoStatus.SUCCESS,
                        "Started today's pickup" + (first?.let { " for ${it.academyName}, which ends at ${KstFormat.hm(it.endTime)}" } ?: "") +
                            ". Please choose a pickup spot on the RideMate screen.",
                    )
                }
                "PICKUP_SPOT" -> {
                    if (!started) return fail(fn, "Please start today's pickup first.")
                    if (screen in AFTER_PICKUP) return fail(fn, "The child has already been picked up.")
                    val p = session.pickupPoint ?: return fail(fn, "Please choose a pickup spot on the RideMate screen first.")
                    val n = navi ?: return fail(fn, "The navigation is not connected yet.")
                    n.requestRoute(NaviPlace(pName, p.lat, p.lng))
                    session.departedAt = session.departedAt ?: now()
                    goTo(Screen.PICKUP_GUIDE)
                    GleoResult(fn, GleoStatus.SUCCESS, "Started navigation to $pName.")
                }
                "WAITING_PLACE" -> {
                    if (!started) return fail(fn, "Please start today's pickup first.")
                    if (screen !in BEFORE_DEPARTURE && screen != Screen.WAITING_DRIVE) return fail(fn, "A waiting place can only be chosen before leaving for the pickup spot.")
                    // 추천 API가 느릴 수 있다: 3초 안에 못 받으면 추천 화면만 열고 실패로 답한다(Gleo 7초 취소 대비)
                    val place = session.waitingPlace ?: withTimeoutOrNull(GleoProtocol.BACKEND_BUDGET_MS) {
                        pickupRepository.getWaitingPlaces().getOrNull()?.places?.let { list -> list.firstOrNull { it.recommended } ?: list.firstOrNull() }
                    }
                    if (place == null) {
                        goTo(Screen.WAITING_PLACE)
                        return fail(fn, "Waiting place recommendations are taking longer than usual. Please choose one on the RideMate screen.")
                    }
                    val n = navi ?: return fail(fn, "The navigation is not connected yet.")
                    n.requestRoute(NaviPlace(place.name, place.latitude, place.longitude))
                    session.waitingPlace = place
                    session.departedAt = session.departedAt ?: now()
                    goTo(Screen.WAITING_DRIVE)
                    GleoResult(fn, GleoStatus.SUCCESS, "Started navigation to the waiting place ${place.name}.")
                }
                "HOME" -> {
                    // 집 경로 = 운전자 탑승 확인. 픽업 장소에 도착한 뒤에만 유효
                    if (screen != Screen.PICKUP_STOP && session.arrivedAtPickupAt == null) {
                        return fail(fn, "The car has not arrived at the pickup spot yet.")
                    }
                    val n = navi ?: return fail(fn, "The navigation is not connected yet.")
                    n.requestRoute(
                        setupStore.home?.let { NaviPlace("집", it.latitude, it.longitude, it.address) }
                            ?: n.state.value.homeBookmark
                            ?: NaviPlace("집(시연)", DemoScenario.HOME.lat, DemoScenario.HOME.lng)
                    )
                    session.boardedAt = session.boardedAt ?: now()
                    goTo(Screen.HOME_DRIVE)
                    GleoResult(fn, GleoStatus.SUCCESS, "Started navigation home.")
                }
                else -> GleoResult(
                    fn, GleoStatus.ASKING, "Should I start today's pickup, or go to the pickup spot, a waiting place, or home?",
                    JSONArray().put(JSONObject().put("missingField", "step")),
                )
            }

            // 3) 외부 요청(아이에게 문자): 호출자가 확인되지 않으면 실행하지 않고 운전자가 화면에서 확인한 뒤 보낸다
            GleoProtocol.FN_SEND_PICKUP_MESSAGE -> {
                val type = req.params.optString("messageType")
                val (valid, label) = when (type) {
                    "PICKUP_SPOT" -> (screen == Screen.PICKUP_GUIDE || screen == Screen.SEND_LINK || screen == Screen.PICKUP_STOP) to "픽업 장소 링크"
                    "ARRIVED" -> (screen == Screen.PICKUP_STOP) to "도착 알림"
                    else -> return GleoResult(
                        fn, GleoStatus.ASKING, "Should I send the pickup spot link, or remind the child that the car has arrived?",
                        JSONArray().put(JSONObject().put("missingField", "messageType")),
                    )
                }
                if (!valid) {
                    return fail(fn, if (type == "ARRIVED") "The car has not arrived at the pickup spot yet." else "The pickup trip has not started yet.")
                }
                if (type == "PICKUP_SPOT") session.linkSentAt?.let {
                    return GleoResult(fn, GleoStatus.SUCCESS, "The pickup spot link was already sent at ${KstFormat.hm(it.toLocalTime())}.")
                }
                suspend fun send(): Boolean =
                    if (type == "ARRIVED") meetingRepository.remindArrived().isSuccess
                    else meetingRepository.sendLink().onSuccess {
                        session.linkSentAt = now()
                        if (backStack.last() == Screen.SEND_LINK) {
                            val fromGuide = backStack.size >= 2 && backStack[backStack.lastIndex - 1] == Screen.PICKUP_GUIDE
                            if (fromGuide && session.arrivedAtPickupAt == null) pop() else replaceTop(Screen.PICKUP_STOP)
                        }
                    }.isSuccess
                if (req.caller.verified) {
                    val ok = withTimeoutOrNull(GleoProtocol.BACKEND_BUDGET_MS) { send() }
                    return when (ok) {
                        true -> GleoResult(fn, GleoStatus.SUCCESS, if (type == "ARRIVED") "I reminded the child that the car is waiting at $pName." else "I sent the pickup spot link ($pName) to the child.")
                        false -> fail(fn, "Failed to send the message. Please try again.")
                        null -> fail(fn, "Sending the message is taking longer than usual. Please check the RideMate screen.")
                    }
                }
                gleoConfirm = GleoConfirm(label) {
                    scope.launch { if (!send()) toast("$label 을(를) 보내지 못했어요") }
                }
                GleoResult(
                    fn, GleoStatus.SUCCESS,
                    "Please confirm on the RideMate screen to send the message to the child.",
                    JSONArray().put(JSONObject().put("executed", false).put("waitingForDriverConfirmation", true)),
                )
            }

            else -> null
        }
    }
    DisposableEffect(Unit) {
        GleoBridge.handler.value = GleoActionHandler { req -> handleGleo(req) }
        onDispose { GleoBridge.handler.value = null }
    }
    // 확인되지 않은 호출자의 외부 요청 → 운전자 확인(디자인 외 요소: 보안상 필요)
    gleoConfirm?.let { c ->
        androidx.compose.material3.AlertDialog(
            onDismissRequest = { gleoConfirm = null },
            title = { androidx.compose.material3.Text("Gleo 요청") },
            text = { androidx.compose.material3.Text("아이에게 ${c.label}을(를) 보낼까요?") },
            confirmButton = {
                androidx.compose.material3.TextButton(onClick = { gleoConfirm = null; c.onConfirm() }) { androidx.compose.material3.Text("보내기") }
            },
            dismissButton = {
                androidx.compose.material3.TextButton(onClick = { gleoConfirm = null }) { androidx.compose.material3.Text("취소") }
            },
        )
    }

    // 로그인 VM은 화면 밖에서도 QR 폴링 시작·중단을 관리해야 해서 여기서 만든다(로그인 화면들과 같은 인스턴스)
    val authVm: AuthViewModel = viewModel(factory = AuthViewModel.Factory(authRepository))
    val qrActive = current == Screen.LOGIN_QR || current == Screen.LOGIN_CONFIRM
    LaunchedEffect(qrActive) { if (qrActive) authVm.startQr() else authVm.stopQr() }

    val pickupFactory = remember(pickupRepository) { PickupViewModelFactory(pickupRepository) }
    val meetingFactory = remember(meetingRepository) { MeetingViewModelFactory(meetingRepository) }
    val driveFactory = remember(driveRepository) { DriveViewModelFactory(driveRepository, recordStore) }

    when (current) {
        Screen.HOME -> {
            val vm: HomeViewModel = viewModel(factory = HomeViewModel.Factory(homeRepository))
            val state by vm.state.collectAsState()
            HomeScreen(
                state = state,
                onOpenTodayPickup = { session.reset(); push(Screen.TODAY_PICKUP) },
                onOpenTimetable = { push(Screen.SETUP_TIMETABLE) },
                onRetry = vm::refresh,
            )
        }

        Screen.TODAY_PICKUP -> {
            val vm: TodayPickupViewModel = viewModel(factory = pickupFactory)
            val state by vm.state.collectAsState()
            LaunchedEffect(state) {
                (state as? LoadState.Content)?.data?.let { d ->
                    session.academyName = d.item.academyName
                    session.endTime = d.item.endTime
                    session.toHomeMin = d.pickupToHomeMin
                }
            }
            TodayPickupScreen(
                state = state,
                onChangeHome = { push(Screen.SETUP_HOME) },
                onChoosePickupSpot = { push(Screen.PICKUP_SPOT) },
                onRetry = vm::refresh,
            )
        }

        Screen.PICKUP_SPOT -> {
            val vm: PickupSpotViewModel = viewModel(factory = pickupFactory)
            val state by vm.state.collectAsState()
            PickupSpotScreen(
                state = state,
                onBack = ::pop,
                onSelect = vm::select,
                onConfirm = { spot ->
                    session.chosenSpot = spot
                    session.pickupName = spot.name
                    session.pickupPoint = GeoPoint(spot.latitude, spot.longitude)
                    session.walkMin = spot.walkMin
                    // 정본 AddPickupPlan(멱등): 부모가 고른 좌표로 오늘 계획 확정. 실패해도 진행하고 출발 시 다시 시도
                    scope.launch {
                        pickupRepository.ensurePickupPlan(GeoPoint(spot.latitude, spot.longitude))
                            .onSuccess { p -> p?.let(session::applyPlan) }
                            .onFailure { e -> toast(e.message ?: "픽업 계획을 만들지 못했어요") }
                    }
                    val departure = (state.load as? LoadState.Content)?.data?.departureDt
                    session.departureDt = departure ?: session.departureDt
                    when (departure?.let { PickupFlowRules.afterSpotSelected(now(), it) }) {
                        PickupFlowRules.AfterSpotSelected.WAITING_PLACE -> push(Screen.WAITING_PLACE)
                        PickupFlowRules.AfterSpotSelected.BEFORE_DEPARTURE, null -> push(Screen.DEPARTURE)
                    }
                },
                onCustomSpot = { notReady("직접 장소 입력") },
                onRetry = vm::refresh,
            )
        }

        Screen.DEPARTURE -> {
            val vm: DepartureViewModel = viewModel(factory = pickupFactory)
            val state by vm.state.collectAsState()
            val gear by rememberGearState()
            LaunchedEffect(state) {
                (state as? LoadState.Content)?.data?.let { p ->
                    session.applyDeparture(p)
                    setupStore.cacheDeparture(p.departureDt) // 시동 알림 문구용
                }
            }
            // 정본 B-1: 추천 출발 30분 전부터 화면이 떠 있는 동안 1분 간격 재조회
            LaunchedEffect(Unit) {
                while (true) {
                    delay(60_000)
                    val dep = session.departureDt ?: continue
                    if (Duration.between(now(), dep).toMinutes() <= 30) vm.refreshQuietly()
                }
            }
            DepartureScreen(
                state = state,
                isParked = gear.isParked(),
                onStartRoute = { plan ->
                    // 정본 B-2: 계획이 아직 없으면(30분 전 호출 실패 등) 먼저 만든다(정책 확인 필요 항목)
                    if (session.plan == null) scope.launch {
                        pickupRepository.ensurePickupPlan(session.chosenSpot?.let { GeoPoint(it.latitude, it.longitude) })
                            .onSuccess { p -> p?.let(session::applyPlan) }
                    }
                    session.departedAt = now()
                    routeTo(session.pickupName ?: plan.pickupName, session.pickupPoint ?: GeoPoint(plan.pickupLatitude, plan.pickupLongitude))
                    push(Screen.PICKUP_GUIDE)
                },
                onRetry = vm::refresh,
            )
        }

        Screen.WAITING_PLACE -> {
            val vm: WaitingPlaceViewModel = viewModel(factory = pickupFactory)
            val state by vm.state.collectAsState()
            WaitingPlaceScreen(
                state = state,
                onSelect = vm::select,
                onStartRoute = { place ->
                    session.waitingPlace = place
                    session.departedAt = now()
                    routeTo(place.name, GeoPoint(place.latitude, place.longitude))
                    push(Screen.WAITING_DRIVE)
                },
                onRetry = vm::refresh,
                // 바로 시작: 대기 장소를 거치지 않고 지금 위치에서 픽업 장소로 → 픽업 장소 안내(이동 중)
                onStartNow = {
                    session.waitingPlace = null
                    session.departedAt = now()
                    routeTo(pickupName, pickupPoint)
                    push(Screen.PICKUP_GUIDE)
                },
            )
        }

        Screen.WAITING_DRIVE -> {
            val vm: WaitingDriveViewModel = viewModel(factory = driveFactory)
            // 개발용 직접 진입 시(선택 없음) 추천 1번 Mock 장소 사용
            val place = session.waitingPlace ?: DEV_WAITING_PLACE
            LaunchedEffect(place) { vm.load(place) }
            val state by vm.state.collectAsState()
            val arrived = rememberArrived(GeoPoint(place.latitude, place.longitude), place.name)
            fun onArrived() { session.waitingPlace = place; session.waitingArrivedAt = now(); replaceTop(Screen.WAITING_STAY) }
            LaunchedEffect(arrived) { if (arrived) onArrived() }
            WaitingDriveScreen(
                state = state.withLiveProgress(DemoScenario.TO_WAITING) { d, p -> d.copy(progress = p) },
                onRetry = vm::retry,
                onDevAdvance = ::onArrived,
            )
        }

        Screen.WAITING_STAY -> {
            val place = session.waitingPlace ?: DEV_WAITING_PLACE
            // 대기 장소 → 픽업 장소 차량 소요: NaviHelper requestTrip(TripInfo.eta) 우선, 없으면 후보 데이터(Mock)
            LaunchedEffect(pickupPoint) { navi?.requestTrip(NaviPlace(pickupName, pickupPoint.lat, pickupPoint.lng)) }
            val driveMin = naviState.tripEtaSec?.let { (it + 59) / 60 } ?: place.driveMin
            val endTime = session.endTime ?: java.time.LocalTime.of(22, 0)
            val target = session.plan?.slotDt?.toLocalTime() ?: endTime.minusMinutes(5)
            val today = now().toLocalDate()
            WaitingStayScreen(
                info = WaitingStayInfo(
                    place = place,
                    placeName = place.name,
                    departAt = driveMin?.let { today.atTime(target).minusMinutes(it.toLong()) },
                    endTime = endTime,
                    arriveBeforeMin = Duration.between(target, endTime).toMinutes().toInt(),
                    driveToPickupMin = driveMin,
                    pickupName = pickupName,
                    pickupArrive = target,
                    toHomeMin = session.toHomeMin,
                    homeArrive = session.toHomeMin?.let { maxOf(target, endTime).plusMinutes((session.walkMin ?: 0).toLong() + it) },
                    feeText = place.feeDetail ?: place.feeText,
                    isMock = true, // 대기 장소·요금은 정본 API 없음
                ),
                onDepartNow = {
                    routeTo(pickupName, pickupPoint)
                    replaceTop(Screen.PICKUP_GUIDE)
                },
            )
        }

        Screen.PICKUP_GUIDE -> {
            val start = session.waitingPlace?.let { GeoPoint(it.latitude, it.longitude) } ?: DemoScenario.WAITING
            val live = LiveDriveTracker.compute(LocalLocationState.current, naviState, listOf(start, pickupPoint))
            val remain = live.progress.remainMin
            val walk = session.walkMin
            fun onArrived() {
                session.arrivedAtPickupAt = now()
                replaceTop(if (session.linkSentAt != null) Screen.PICKUP_STOP else Screen.SEND_LINK)
            }
            // 만남 연결 시점: 남은 시간 ≤ 아이 도보 + 1분 (디자인 "보내기 좋은 시점")
            LaunchedEffect(remain, walk) {
                if (session.linkSentAt == null && !session.sendPromptDismissed && remain != null && walk != null && remain <= walk + 1) {
                    push(Screen.SEND_LINK)
                }
            }
            val arrived = rememberArrived(pickupPoint, pickupName)
            LaunchedEffect(arrived) { if (arrived) onArrived() }
            PickupGuideScreen(
                info = PickupGuideInfo(
                    assignedArrival = session.plan?.slotDt?.toLocalTime(),
                    endTime = session.endTime,
                    pickupName = pickupName,
                    walkMin = walk,
                    restricted = session.restricted == true,
                    guideText = session.plan?.guideText,
                    remainMin = remain,
                    distanceKm = live.progress.distanceKm,
                    isMock = session.plan?.isMock != false,
                ),
                naviGuiding = naviState.routing,
                onDevAdvance = ::onArrived,
            )
        }

        Screen.SEND_LINK -> {
            val vm: SendLinkViewModel = viewModel(factory = meetingFactory)
            val state by vm.state.collectAsState()
            val fromGuide = backStack.size >= 2 && backStack[backStack.lastIndex - 1] == Screen.PICKUP_GUIDE
            LaunchedEffect(state.sentEvent) {
                if (state.sentEvent) {
                    vm.consumeSentEvent()
                    session.linkSentAt = now()
                    // 주행 중 보냈으면 안내 화면으로 돌아가 도착을 기다리고, 이미 도착했으면 정차 화면으로
                    if (fromGuide && session.arrivedAtPickupAt == null) pop() else replaceTop(Screen.PICKUP_STOP)
                }
            }
            SendLinkScreen(
                state = state,
                onSend = vm::send,
                onLater = {
                    session.sendPromptDismissed = true
                    if (fromGuide && session.arrivedAtPickupAt == null) pop() else replaceTop(Screen.PICKUP_STOP)
                },
                onRetry = vm::refresh,
                walkMin = session.walkMin,
                remainMin = naviState.remainSec?.takeIf { naviState.routing }?.let { (it + 59) / 60 },
            )
        }

        Screen.PICKUP_STOP -> {
            val vm: PickupStopViewModel = viewModel(factory = meetingFactory)
            val state by vm.state.collectAsState()
            val doors = remember { RearDoorMonitor(context) }
            DisposableEffect(doors) {
                doors.start()
                onDispose { doors.stop() }
            }
            val doorState by doors.state.collectAsState()
            var devBoarding by remember { mutableStateOf(false) }
            PickupStopScreen(
                state = state,
                onRemind = vm::remindArrived,
                onRetry = vm::refresh,
                walkMin = session.walkMin,
                boardingCandidate = doorState.boardingCandidateCount > 0 || devBoarding,
                onConfirmBoarded = {
                    session.boardedAt = now()
                    val home = homePlace()
                    navi?.requestRoute(home) ?: toast("내비 연결 전이에요")
                    replaceTop(Screen.HOME_DRIVE)
                },
                onDevBoarding = { devBoarding = true },
            )
        }

        Screen.HOME_DRIVE -> {
            val vm: HomeDriveViewModel = viewModel(factory = driveFactory)
            val state by vm.state.collectAsState()
            val home = homePlace()
            val arrived = rememberArrived(GeoPoint(home.latitude, home.longitude), home.name)
            fun onArrived() { session.homeArrivedAt = now(); replaceTop(Screen.RECORD) }
            LaunchedEffect(arrived) { if (arrived) onArrived() }
            HomeDriveScreen(
                state = state.withLiveProgress(DemoScenario.TO_HOME) { d, p -> d.copy(progress = p) },
                onRetry = vm::refresh,
                onDevAdvance = ::onArrived,
            )
        }

        Screen.RECORD -> {
            val vm: RecordViewModel = viewModel(factory = driveFactory)
            val state by vm.state.collectAsState()
            LaunchedEffect(state.doneEvent) {
                if (state.doneEvent) {
                    vm.consumeDone()
                    session.reset()
                    resetToHome()
                }
            }
            RecordScreen(
                state = state,
                onAutoSaveChange = vm::setAutoSave,
                onSave = vm::save,
                onSkip = vm::skip,
                onRetry = vm::refresh,
            )
        }

        Screen.LOGIN_QR, Screen.LOGIN_PHONE, Screen.LOGIN_OTP, Screen.LOGIN_CONFIRM -> {
            val vm = authVm
            val state by vm.state.collectAsState()
            LaunchedEffect(state.event) {
                when (state.event) {
                    AuthEvent.OTP_SENT -> if (current != Screen.LOGIN_OTP) push(Screen.LOGIN_OTP)
                    AuthEvent.QR_VERIFIED -> if (current == Screen.LOGIN_QR) push(Screen.LOGIN_CONFIRM)
                    AuthEvent.QR_RESET -> if (current == Screen.LOGIN_CONFIRM) pop()
                    AuthEvent.SIGNUP_REQUIRED -> {
                        // 차량에서는 가입 불가 → QR 로그인 화면으로
                        val i = backStack.lastIndexOf(Screen.LOGIN_QR)
                        if (i >= 0) {
                            while (backStack.size > i + 1) pop()
                        } else {
                            push(Screen.LOGIN_QR)
                        }
                    }
                    AuthEvent.LOGGED_IN_EXISTING -> { setupStore.loggedIn = true; replaceAll(Screen.HOME) }
                    AuthEvent.LOGGED_IN_SETUP_REQUIRED -> { setupStore.loggedIn = true; replaceAll(Screen.SETUP_CHILD) }
                    AuthEvent.LOGGED_OUT -> { setupStore.loggedIn = false; toast("이 차에서 로그아웃했어요") }
                    null -> Unit
                }
                if (state.event != null) vm.consumeEvent()
            }
            when (current) {
                Screen.LOGIN_QR -> {
                    // 명세: 차량 출발 시 QR 취소(DRIVING) 후 폴링 중단
                    val gear by rememberGearState()
                    LaunchedEffect(gear) { if (gear != null && !gear.isParked()) vm.cancelForDriving() }
                    QrLoginScreen(
                        state, onAccount = vm::loginWith, onPhoneLogin = { push(Screen.LOGIN_PHONE) }, onLogout = vm::logoutThisCar,
                        onRefreshQr = vm::refreshQr, onQrScannedDemo = vm::qrScannedDemo,
                    )
                }
                Screen.LOGIN_CONFIRM -> {
                    // 취소 → 이 QR 취소(USER) 후 새 QR, QR_RESET 이벤트로 QR 화면 복귀
                    BackHandler { vm.cancelConfirm() }
                    LoginConfirmScreen(state, onCancel = vm::cancelConfirm, onApprovedDemo = vm::phoneApproved)
                }
                Screen.LOGIN_PHONE -> PhoneLoginScreen(state, vm::phoneDigit, vm::phoneBackspace, vm::phoneClear, vm::requestOtp, onBack = ::pop)
                else -> OtpScreen(state, vm::otpDigit, vm::otpBackspace, vm::otpClear, onResend = vm::requestOtp, onConfirm = vm::verifyOtp, onBack = ::pop)
            }
        }

        Screen.SETUP_CHILD, Screen.SETUP_TIMETABLE, Screen.SETUP_HOME, Screen.SETUP_DONE -> {
            val vm: SetupViewModel = viewModel(factory = SetupViewModel.Factory(setupRepository))
            val state by vm.state.collectAsState()
            LaunchedEffect(state.event) {
                when (state.event) {
                    SetupEvent.CHILD_SAVED -> push(Screen.SETUP_TIMETABLE)
                    SetupEvent.HOME_SAVED -> { vm.loadSummary(); replaceAll(Screen.SETUP_DONE) }
                    null -> Unit
                }
                if (state.event != null) vm.consumeEvent()
            }
            // 내비 '집' 즐겨찾기는 내비 연결 뒤 비동기로 도착 → 받으면 다시 읽는다
            LaunchedEffect(naviState.homeBookmark) { vm.reloadNaviHome() }
            when (current) {
                Screen.SETUP_CHILD -> ChildInfoScreen(state, onChange = vm::updateChild, onNext = vm::saveChild)
                Screen.SETUP_TIMETABLE -> TimetableScreen(
                    state, onSelectDay = vm::selectDay, onToggle = vm::togglePickup,
                    onAddAcademy = { notReady("학원 추가(입력 화면 디자인 필요)") }, onNext = { push(Screen.SETUP_HOME) },
                )
                Screen.SETUP_HOME -> HomeSetupScreen(
                    state,
                    onUseNaviHome = vm::confirmNaviHome,
                    onSearchOther = { notReady("주소 검색(검색 API 미정)") },
                    onDone = vm::saveHome,
                )
                else -> {
                    LaunchedEffect(Unit) { if (state.summary == null) vm.loadSummary() }
                    SetupDoneScreen(state, onGoHome = { replaceAll(Screen.HOME) })
                }
            }
        }
    }
}

/**
 * 목적지 도착 판정: ① NaviHelper onDestinationArrived(이번에 요청한 목적지) ② FusedLocation 위치가 반경 50m 이내.
 * 둘 중 하나면 도착. (②는 내비 이벤트가 없는 에뮬레이터 GPS 시연 보조용)
 */
@Composable
private fun rememberArrived(target: GeoPoint, destinationName: String): Boolean {
    val navi = LocalNaviState.current
    val vehicle = LocalVehicleLocation.current
    val byNavi = navi.arrived != null && navi.destination?.name == destinationName
    val byGps = vehicle != null && RouteMath.distanceM(vehicle, target) <= ARRIVAL_RADIUS_M
    return byNavi || byGps
}

/** 화면이 보이는 동안만 기어 상태를 구독한다. */
@Composable
private fun rememberGearState(): State<VehicleGear?> {
    val context = LocalContext.current
    val monitor = remember { GearMonitor(context) }
    DisposableEffect(monitor) {
        monitor.start()
        onDispose { monitor.stop() }
    }
    return monitor.gear.collectAsState()
}

/** 개발용 직접 진입(start_screen=WAITING_DRIVE) 시 사용하는 Mock 대기 장소 */
private val DEV_WAITING_PLACE = WaitingPlace(
    1, "도곡초교 공영주차장", true, 5, "약 2,100원", 120, 37.49360, 127.05780, feeDetail = "기본 600원 · 10분 당 300원",
)
