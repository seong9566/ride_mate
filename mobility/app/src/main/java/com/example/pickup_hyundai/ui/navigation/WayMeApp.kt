package com.example.pickup_hyundai.ui.navigation

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
) {
    val context = LocalContext.current
    val navi = LocalNaviController.current
    val naviState = LocalNaviState.current
    val scope = rememberCoroutineScope()
    val session = remember { PickupSession() }

    val backStack = remember { if (startScreen == Screen.HOME) mutableStateListOf(Screen.HOME) else mutableStateListOf(Screen.HOME, startScreen) }
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
            val vm: AuthViewModel = viewModel(factory = AuthViewModel.Factory(authRepository))
            val state by vm.state.collectAsState()
            LaunchedEffect(state.event) {
                when (state.event) {
                    AuthEvent.OTP_SENT -> if (current != Screen.LOGIN_OTP) push(Screen.LOGIN_OTP)
                    AuthEvent.CONFIRM_REQUESTED -> if (current != Screen.LOGIN_CONFIRM) push(Screen.LOGIN_CONFIRM)
                    AuthEvent.LOGGED_IN_EXISTING -> replaceAll(Screen.HOME)
                    AuthEvent.LOGGED_IN_SETUP_REQUIRED -> replaceAll(Screen.SETUP_CHILD)
                    AuthEvent.LOGGED_OUT -> notReady("이 차에서 로그아웃(Mock)")
                    null -> Unit
                }
                if (state.event != null) vm.consumeEvent()
            }
            when (current) {
                Screen.LOGIN_QR -> QrLoginScreen(
                    state, onAccount = vm::loginWith, onPhoneLogin = { push(Screen.LOGIN_PHONE) }, onLogout = vm::logoutThisCar,
                    onRefreshQr = vm::refreshQr, onQrScannedDemo = vm::qrScannedDemo,
                )
                Screen.LOGIN_CONFIRM -> {
                    BackHandler { vm.cancelConfirm(); pop() }
                    LoginConfirmScreen(state, onCancel = { vm.cancelConfirm(); pop() }, onApprovedDemo = vm::phoneApproved)
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
