package com.example.pickup_hyundai.ui.auth

import androidx.lifecycle.ViewModel
import androidx.lifecycle.ViewModelProvider
import androidx.lifecycle.viewModelScope
import com.example.pickup_hyundai.data.repository.AuthRepository
import com.example.pickup_hyundai.domain.model.LoginAccount
import com.example.pickup_hyundai.domain.model.LoginOutcome
import com.example.pickup_hyundai.domain.model.OtpRequest
import com.example.pickup_hyundai.domain.model.OtpVerify
import com.example.pickup_hyundai.domain.model.QrPoll
import kotlinx.coroutines.Job
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch

enum class AuthEvent {
    LOGGED_IN_EXISTING, LOGGED_IN_SETUP_REQUIRED, OTP_SENT,
    /** 휴대폰에서 본인 확인 완료(qr_status=VERIFIED) → "휴대폰에서 확인하고 있어요" 화면 */
    QR_VERIFIED,
    /** 확인 화면에서 QR 화면으로 돌아가야 함(취소·만료) */
    QR_RESET,
    /** 차량 OTP: 미가입 번호 → QR 로그인으로 */
    SIGNUP_REQUIRED,
    LOGGED_OUT,
}

data class AuthUiState(
    val accounts: List<LoginAccount> = emptyList(),
    /** QR에 넣을 주소(verification_uri_complete). null = 발급 전 */
    val qrContent: String? = null,
    /** 차·휴대폰에 함께 보이는 4자리(문자열, 앞자리 0 유지) */
    val matchCode: String? = null,
    /** PENDING / SCANNED / VERIFIED */
    val qrStatus: String? = null,
    val qrRemainSec: Int = 0,
    /** QR 영역 안내(새 QR로 바꿈, 휴대폰에서 취소 등) */
    val qrNotice: String? = null,
    val phoneDigits: String = "",
    val otp: String = "",
    val otpRemainSec: Int = 0,
    val busy: Boolean = false,
    val error: String? = null,
    val event: AuthEvent? = null,
    val isMock: Boolean = true,
)

/** A00 로그인(QR · 계정 선택 · 휴대폰 OTP) — WAY:ME 인증 API M-01~M-08 */
class AuthViewModel(private val repository: AuthRepository) : ViewModel() {
    private val _state = MutableStateFlow(AuthUiState(isMock = repository.isMock))
    val state: StateFlow<AuthUiState> = _state.asStateFlow()
    private var qrJob: Job? = null
    private var qrTimer: Job? = null
    private var otpTimer: Job? = null

    init { loadAccounts() }

    fun loadAccounts() {
        viewModelScope.launch {
            repository.getAccounts().fold(
                { list -> _state.update { it.copy(accounts = list) } },
                { e -> _state.update { it.copy(error = e.message) } },
            )
        }
    }

    // ───────── QR 로그인(M-01 발급 → M-02 폴링 → M-03 취소) ─────────

    /** QR 화면이 보일 때 호출. 이미 돌고 있으면 그대로 */
    fun startQr() {
        if (qrJob?.isActive == true) return
        qrJob = viewModelScope.launch { runQr() }
    }

    /** 새로고침 버튼: 이전 QR은 발급 API 안에서 USER로 취소된다 */
    fun refreshQr() {
        stopLoops()
        _state.update { it.copy(qrNotice = null) }
        startQr()
    }

    /** QR 화면을 떠날 때(휴대폰 번호 로그인 등) — 폴링 중단 + 서버 취소(USER) */
    fun stopQr() = stopQrWith("USER")

    /** 차량 출발(P단 해제) — 서버 취소(DRIVING) */
    fun cancelForDriving() {
        if (qrJob?.isActive != true) return
        stopQrWith("DRIVING")
        _state.update { it.copy(qrNotice = "주행 중에는 QR 로그인을 멈춰요. 정차 후 다시 시도해 주세요") }
    }

    /** 확인 화면의 "취소": 이 QR을 취소하고 새 QR */
    fun cancelConfirm() {
        stopQrWith("USER")
        _state.update { it.copy(event = AuthEvent.QR_RESET) }
        startQr()
    }

    private fun stopQrWith(reason: String) {
        val wasActive = qrJob?.isActive == true
        stopLoops()
        _state.update { it.copy(qrContent = null, matchCode = null, qrStatus = null, qrRemainSec = 0) }
        if (wasActive) viewModelScope.launch { repository.cancelQr(reason) }
    }

    private fun stopLoops() {
        qrJob?.cancel(); qrJob = null
        qrTimer?.cancel(); qrTimer = null
    }

    private suspend fun runQr() {
        while (viewModelScope.isActive) {
            val issued = repository.issueQr()
            if (issued.isFailure) {
                _state.update { it.copy(qrNotice = issued.exceptionOrNull()?.message ?: "QR을 만들지 못했어요") }
                delay(RETRY_MS)
                continue
            }
            val qr = issued.getOrThrow()
            _state.update { it.copy(qrContent = qr.qrContent, matchCode = qr.matchCode, qrStatus = "PENDING", qrRemainSec = qr.expiresInSec, error = null) }
            qrTimer?.cancel()
            qrTimer = viewModelScope.launch {
                while (_state.value.qrRemainSec > 0) { delay(1_000); _state.update { it.copy(qrRemainSec = it.qrRemainSec - 1) } }
            }
            var interval = qr.intervalSec * 1_000L
            var renew = false
            while (!renew) {
                delay(interval)
                val polled = repository.pollQr()
                if (polled.isFailure) continue // 네트워크 오류: 다음 간격에 다시
                val wasVerified = _state.value.qrStatus == "VERIFIED"
                when (val p = polled.getOrThrow()) {
                    is QrPoll.Pending -> {
                        _state.update { it.copy(qrStatus = p.qrStatus) }
                        if (p.qrStatus == "VERIFIED" && !wasVerified) _state.update { it.copy(event = AuthEvent.QR_VERIFIED) }
                        // 승인 전이 아닌데 3분이 지나면 새 QR(서버도 expired_token을 준다)
                        if (p.qrStatus != "VERIFIED" && _state.value.qrRemainSec <= 0) renew = true
                    }
                    QrPoll.SlowDown -> interval += SLOW_DOWN_STEP_MS
                    QrPoll.Expired, QrPoll.Invalid -> {
                        _state.update { it.copy(qrNotice = "QR 유효시간이 지나 새 QR로 바꿨어요", event = if (wasVerified) AuthEvent.QR_RESET else it.event) }
                        renew = true
                    }
                    is QrPoll.Denied -> {
                        val msg = when (p.reason) {
                            "PHONE" -> "휴대폰에서 로그인을 취소했어요"
                            "DRIVING" -> "차량이 출발해 QR 로그인을 멈췄어요"
                            else -> "로그인을 취소했어요"
                        }
                        _state.update { it.copy(qrNotice = msg, event = if (wasVerified) AuthEvent.QR_RESET else it.event) }
                        if (p.reason == "DRIVING") { qrTimer?.cancel(); return }
                        renew = true
                    }
                    is QrPoll.Approved -> {
                        qrTimer?.cancel()
                        _state.update { it.copy(qrContent = null, qrStatus = null, event = loggedIn(p.outcome)) }
                        return
                    }
                }
            }
        }
    }

    /** 시연(Mock 전용) */
    fun qrScannedDemo() = repository.demoAdvance()
    fun phoneApproved() = repository.demoAdvance()

    // ───────── 계정 선택(M-04) · 로그아웃(M-05) ─────────

    fun loginWith(account: LoginAccount) {
        if (!account.canResume) {
            _state.update { it.copy(error = "이 차에 ${account.userName} 계정의 로그인 정보가 없어요. QR을 찍어 로그인해 주세요") }
            return
        }
        perform { repository.loginWithAccount(account.id).map { o -> stopLoops(); loggedIn(o) } }
    }

    fun logoutThisCar() = perform {
        repository.logoutThisCar().map { loadAccounts(); AuthEvent.LOGGED_OUT }
    }

    // ───────── 휴대폰 OTP(M-07 발송 → M-08 확인) ─────────

    fun phoneDigit(c: Char) = _state.update { if (it.phoneDigits.length < 11) it.copy(phoneDigits = it.phoneDigits + c, error = null) else it }
    fun phoneBackspace() = _state.update { it.copy(phoneDigits = it.phoneDigits.dropLast(1)) }
    fun phoneClear() = _state.update { it.copy(phoneDigits = "") }

    fun requestOtp() {
        if (!PHONE_REGEX.matches(_state.value.phoneDigits)) {
            _state.update { it.copy(error = "휴대폰 번호를 확인해 주세요") }
            return
        }
        if (_state.value.busy) return
        _state.update { it.copy(busy = true, error = null) }
        viewModelScope.launch {
            repository.requestOtp(_state.value.phoneDigits).fold(
                { r ->
                    when (r) {
                        is OtpRequest.Sent -> {
                            _state.update { it.copy(busy = false, otp = "", event = AuthEvent.OTP_SENT) }
                            startOtpTimer(r.expiresInSec)
                        }
                        is OtpRequest.TooSoon -> _state.update { it.copy(busy = false, error = "${r.retryAfterSec}초 뒤에 다시 받을 수 있어요") }
                        is OtpRequest.Locked -> _state.update { it.copy(busy = false, error = "인증번호를 5번 틀려 잠겼어요. ${(r.retryAfterSec + 59) / 60}분 뒤 다시 시도해 주세요") }
                    }
                },
                { e -> _state.update { it.copy(busy = false, error = e.message ?: "인증번호를 보내지 못했어요") } },
            )
        }
    }

    fun otpDigit(c: Char) = _state.update { if (it.otp.length < 6) it.copy(otp = it.otp + c, error = null) else it }
    fun otpBackspace() = _state.update { it.copy(otp = it.otp.dropLast(1)) }
    fun otpClear() = _state.update { it.copy(otp = "") }

    fun verifyOtp() {
        val s = _state.value
        if (s.otp.length != 6 || s.busy) return
        if (s.otpRemainSec <= 0) {
            _state.update { it.copy(error = "인증번호가 만료됐어요. 다시 받아 주세요") }
            return
        }
        _state.update { it.copy(busy = true, error = null) }
        viewModelScope.launch {
            repository.verifyOtp(s.phoneDigits, s.otp).fold(
                { r ->
                    _state.update { st ->
                        when (r) {
                            is OtpVerify.Success -> { otpTimer?.cancel(); st.copy(busy = false, event = loggedIn(r.outcome)) }
                            is OtpVerify.Mismatch -> st.copy(busy = false, otp = "", error = "인증번호가 맞지 않아요" + (r.remainingAttempts?.let { " (남은 횟수 ${it}번)" } ?: ""))
                            is OtpVerify.Locked -> st.copy(busy = false, otp = "", error = "5번 틀려 잠겼어요." + (r.retryAfterSec?.let { " ${(it + 59) / 60}분 뒤 다시 시도해 주세요" } ?: ""))
                            OtpVerify.Expired -> st.copy(busy = false, otp = "", otpRemainSec = 0, error = "인증번호가 만료됐어요. 다시 받아 주세요")
                            OtpVerify.NotIssued -> st.copy(busy = false, otp = "", error = "인증번호를 다시 받아 주세요")
                            OtpVerify.SignupRequired -> st.copy(busy = false, otp = "", error = "가입되지 않은 번호예요. 휴대폰으로 QR을 찍어 가입·로그인해 주세요", event = AuthEvent.SIGNUP_REQUIRED)
                        }
                    }
                },
                { e -> _state.update { it.copy(busy = false, error = e.message ?: "확인하지 못했어요") } },
            )
        }
    }

    fun consumeEvent() = _state.update { it.copy(event = null) }

    private fun loggedIn(o: LoginOutcome) = if (o.setupRequired) AuthEvent.LOGGED_IN_SETUP_REQUIRED else AuthEvent.LOGGED_IN_EXISTING

    private fun startOtpTimer(sec: Int) {
        otpTimer?.cancel()
        _state.update { it.copy(otpRemainSec = sec) }
        otpTimer = viewModelScope.launch {
            while (_state.value.otpRemainSec > 0) { delay(1_000); _state.update { it.copy(otpRemainSec = it.otpRemainSec - 1) } }
        }
    }

    private fun perform(block: suspend () -> Result<AuthEvent>) {
        if (_state.value.busy) return
        _state.update { it.copy(busy = true, error = null) }
        viewModelScope.launch {
            block().fold(
                { e -> _state.update { it.copy(busy = false, event = e) } },
                { t -> _state.update { it.copy(busy = false, error = t.message ?: "다시 시도해 주세요") } },
            )
        }
    }

    override fun onCleared() {
        // 화면이 완전히 사라지면 진행 중 QR 취소
        if (qrJob?.isActive == true) {
            stopLoops()
        }
    }

    class Factory(private val repository: AuthRepository) : ViewModelProvider.Factory {
        @Suppress("UNCHECKED_CAST")
        override fun <T : ViewModel> create(modelClass: Class<T>): T = AuthViewModel(repository) as T
    }

    private companion object {
        /** 명세 W-03 정규식과 같게 */
        val PHONE_REGEX = Regex("^01[016789][0-9]{7,8}$")
        const val RETRY_MS = 5_000L
        const val SLOW_DOWN_STEP_MS = 5_000L
    }
}
