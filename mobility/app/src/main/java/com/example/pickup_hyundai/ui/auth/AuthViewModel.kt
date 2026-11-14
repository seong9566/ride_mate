package com.example.pickup_hyundai.ui.auth

import androidx.lifecycle.ViewModel
import androidx.lifecycle.ViewModelProvider
import androidx.lifecycle.viewModelScope
import com.example.pickup_hyundai.data.repository.AuthRepository
import com.example.pickup_hyundai.domain.model.LoginAccount
import kotlinx.coroutines.Job
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch

enum class AuthEvent { LOGGED_IN_EXISTING, LOGGED_IN_SETUP_REQUIRED, OTP_SENT, CONFIRM_REQUESTED, LOGGED_OUT }

data class AuthUiState(
    val accounts: List<LoginAccount> = emptyList(),
    /** QR 유효 남은 시간(초). 차량용 QR 발급 API가 정본에 없어 Mock 5분 */
    val qrRemainSec: Int = 0,
    val phoneDigits: String = "",
    val otp: String = "",
    val otpRemainSec: Int = 0,
    /** 휴대폰 확인 화면의 4자리 숫자(null = 확인 대기 아님) */
    val matchCode: String? = null,
    val confirmRemainSec: Int = 0,
    val busy: Boolean = false,
    val error: String? = null,
    val event: AuthEvent? = null,
    val isMock: Boolean = true,
)

/** A00 로그인(QR · 휴대폰 · 인증번호 · 휴대폰 확인) 공용 ViewModel */
class AuthViewModel(private val repository: AuthRepository) : ViewModel() {
    private val _state = MutableStateFlow(AuthUiState())
    val state: StateFlow<AuthUiState> = _state.asStateFlow()
    private var otpTimer: Job? = null
    private var qrTimer: Job? = null
    private var confirmTimer: Job? = null

    init {
        viewModelScope.launch {
            repository.getAccounts().onSuccess { list -> _state.update { it.copy(accounts = list) } }
        }
        refreshQr()
    }

    /** QR 새로 받기(디자인 남은 시간 옆 새로고침) */
    fun refreshQr() {
        qrTimer = countdown(qrTimer, QR_VALID_SEC, { it.copy(qrRemainSec = QR_VALID_SEC) }, { it.qrRemainSec }) { it.copy(qrRemainSec = it.qrRemainSec - 1) }
    }

    /** 이 차에 로그인한 계정 선택 → 휴대폰에서 허용 확인 */
    fun loginWith(account: LoginAccount) = requestConfirm(account.id)

    /** 개발·시연: 휴대폰이 QR을 찍은 것으로 처리 */
    fun qrScannedDemo() = requestConfirm(null)

    private fun requestConfirm(accountId: String?) = perform {
        repository.requestLoginConfirm(accountId).map { c ->
            _state.update { it.copy(matchCode = c.matchCode) }
            confirmTimer = countdown(confirmTimer, c.expiresInSec, { it.copy(confirmRemainSec = c.expiresInSec) }, { it.confirmRemainSec }) {
                it.copy(confirmRemainSec = it.confirmRemainSec - 1)
            }
            AuthEvent.CONFIRM_REQUESTED
        }
    }

    /** 휴대폰에서 '로그인 허용' 수신(현재 Mock: 개발용 버튼) */
    fun phoneApproved() {
        if (_state.value.confirmRemainSec <= 0) {
            _state.update { it.copy(error = "시간이 지났어요. 다시 시도해 주세요") }
            return
        }
        perform {
            repository.completeLoginConfirm().map { setupRequired ->
                cancelConfirmInternal()
                if (setupRequired) AuthEvent.LOGGED_IN_SETUP_REQUIRED else AuthEvent.LOGGED_IN_EXISTING
            }
        }
    }

    fun cancelConfirm() = cancelConfirmInternal()

    private fun cancelConfirmInternal() {
        confirmTimer?.cancel()
        _state.update { it.copy(matchCode = null, confirmRemainSec = 0, error = null) }
    }

    fun logoutThisCar() = perform { repository.logoutThisCar().map { AuthEvent.LOGGED_OUT } }

    fun phoneDigit(c: Char) = _state.update { if (it.phoneDigits.length < 11) it.copy(phoneDigits = it.phoneDigits + c, error = null) else it }
    fun phoneBackspace() = _state.update { it.copy(phoneDigits = it.phoneDigits.dropLast(1)) }
    fun phoneClear() = _state.update { it.copy(phoneDigits = "") }

    fun requestOtp() {
        if (!PHONE_REGEX.matches(_state.value.phoneDigits)) {
            _state.update { it.copy(error = "휴대폰 번호 11자리를 입력해 주세요") }
            return
        }
        perform {
            repository.requestOtp(_state.value.phoneDigits).map { sec ->
                _state.update { it.copy(otp = "") }
                otpTimer = countdown(otpTimer, sec, { it.copy(otpRemainSec = sec) }, { it.otpRemainSec }) { it.copy(otpRemainSec = it.otpRemainSec - 1) }
                AuthEvent.OTP_SENT
            }
        }
    }

    fun otpDigit(c: Char) = _state.update { if (it.otp.length < 6) it.copy(otp = it.otp + c, error = null) else it }
    fun otpBackspace() = _state.update { it.copy(otp = it.otp.dropLast(1)) }
    fun otpClear() = _state.update { it.copy(otp = "") }

    fun verifyOtp() {
        val s = _state.value
        if (s.otp.length != 6) return
        if (s.otpRemainSec <= 0) {
            _state.update { it.copy(error = "인증번호가 만료됐어요. 다시 받아 주세요") }
            return
        }
        perform {
            repository.verifyOtp(s.phoneDigits, s.otp).map { setupRequired ->
                otpTimer?.cancel()
                if (setupRequired) AuthEvent.LOGGED_IN_SETUP_REQUIRED else AuthEvent.LOGGED_IN_EXISTING
            }
        }
    }

    fun consumeEvent() = _state.update { it.copy(event = null) }

    /** 1초 간격 카운트다운. 이전 타이머는 취소한다 */
    private fun countdown(
        previous: Job?,
        @Suppress("UNUSED_PARAMETER") sec: Int,
        reset: (AuthUiState) -> AuthUiState,
        remain: (AuthUiState) -> Int,
        tick: (AuthUiState) -> AuthUiState,
    ): Job {
        previous?.cancel()
        _state.update(reset)
        return viewModelScope.launch {
            while (remain(_state.value) > 0) {
                delay(1_000)
                _state.update(tick)
            }
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

    class Factory(private val repository: AuthRepository) : ViewModelProvider.Factory {
        @Suppress("UNCHECKED_CAST")
        override fun <T : ViewModel> create(modelClass: Class<T>): T = AuthViewModel(repository) as T
    }

    private companion object {
        val PHONE_REGEX = Regex("^01[0-9]{9}$")
        const val QR_VALID_SEC = 300
    }
}
