package com.example.pickup_hyundai.ui.setup

import androidx.lifecycle.ViewModel
import androidx.lifecycle.ViewModelProvider
import androidx.lifecycle.viewModelScope
import com.example.pickup_hyundai.data.repository.SetupRepository
import com.example.pickup_hyundai.domain.model.AcademySchedule
import com.example.pickup_hyundai.domain.model.ChildInfoInput
import com.example.pickup_hyundai.domain.model.HomeAddress
import com.example.pickup_hyundai.domain.model.SetupSummary
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import java.time.DayOfWeek
import java.time.LocalDate
import java.time.format.DateTimeFormatter
import java.time.format.ResolverStyle

enum class SetupEvent { CHILD_SAVED, HOME_SAVED }

data class SetupUiState(
    val child: ChildInfoInput = ChildInfoInput(),
    val childError: String? = null,
    val schedules: List<AcademySchedule> = emptyList(),
    val selectedDay: DayOfWeek = LocalDate.now().dayOfWeek,
    val naviHome: HomeAddress? = null,
    val homeConfirmed: Boolean = false,
    val summary: SetupSummary? = null,
    val busy: Boolean = false,
    val error: String? = null,
    val event: SetupEvent? = null,
    val isMock: Boolean = true,
)

/** A01 초기 설정(아이 → 시간표 → 집 → 완료) 공용 ViewModel */
class SetupViewModel(private val repository: SetupRepository) : ViewModel() {
    private val _state = MutableStateFlow(SetupUiState())
    val state: StateFlow<SetupUiState> = _state.asStateFlow()

    init {
        viewModelScope.launch {
            repository.getSchedules().onSuccess { list -> _state.update { it.copy(schedules = list) } }
            repository.getNaviHome().onSuccess { h -> _state.update { it.copy(naviHome = h) } }
        }
    }

    fun updateChild(transform: (ChildInfoInput) -> ChildInfoInput) =
        _state.update { it.copy(child = transform(it.child), childError = null) }

    fun saveChild() {
        val error = validateChild(_state.value.child)
        if (error != null) { _state.update { it.copy(childError = error) }; return }
        perform { repository.saveChild(_state.value.child).map { SetupEvent.CHILD_SAVED } }
    }

    fun selectDay(day: DayOfWeek) = _state.update { it.copy(selectedDay = day) }

    fun togglePickup(id: String, enabled: Boolean) {
        // 낙관적 반영 후 실패 시 되돌림
        _state.update { s -> s.copy(schedules = s.schedules.map { if (it.id == id) it.copy(pickupEnabled = enabled) else it }) }
        viewModelScope.launch {
            repository.setPickupEnabled(id, enabled).onFailure {
                _state.update { s -> s.copy(error = "저장하지 못했어요", schedules = s.schedules.map { if (it.id == id) it.copy(pickupEnabled = !enabled) else it }) }
            }
        }
    }

    /** 내비 '집' 즐겨찾기(onBookmarkInfo)는 연결 후 비동기로 오므로, 도착하면 다시 읽는다 */
    fun reloadNaviHome() {
        viewModelScope.launch { repository.getNaviHome().onSuccess { h -> _state.update { it.copy(naviHome = h) } } }
    }

    fun confirmNaviHome() = _state.update { it.copy(homeConfirmed = true) }

    fun saveHome() {
        val home = _state.value.naviHome ?: return
        perform { repository.saveHome(home).map { SetupEvent.HOME_SAVED } }
    }

    fun loadSummary() {
        viewModelScope.launch { repository.getSummary().onSuccess { s -> _state.update { it.copy(summary = s) } } }
    }

    fun consumeEvent() = _state.update { it.copy(event = null) }

    private fun perform(block: suspend () -> Result<SetupEvent>) {
        if (_state.value.busy) return
        _state.update { it.copy(busy = true, error = null) }
        viewModelScope.launch {
            block().fold(
                { e -> _state.update { it.copy(busy = false, event = e) } },
                { t -> _state.update { it.copy(busy = false, error = t.message ?: "다시 시도해 주세요") } },
            )
        }
    }

    class Factory(private val repository: SetupRepository) : ViewModelProvider.Factory {
        @Suppress("UNCHECKED_CAST")
        override fun <T : ViewModel> create(modelClass: Class<T>): T = SetupViewModel(repository) as T
    }

    companion object {
        private val BIRTH = DateTimeFormatter.ofPattern("uuuuMMdd").withResolverStyle(ResolverStyle.STRICT)

        /** 입력 검증. 정상이면 null */
        fun validateChild(c: ChildInfoInput, today: LocalDate = LocalDate.now()): String? = when {
            c.name.isBlank() -> "이름을 입력해 주세요"
            !Regex("^01[0-9]{9}$").matches(c.phoneDigits) -> "휴대폰 번호 11자리를 입력해 주세요"
            c.birthDigits.length != 8 -> "생년월일 8자리를 입력해 주세요(예: 20100514)"
            runCatching { LocalDate.parse(c.birthDigits, BIRTH) }.getOrNull()?.isAfter(today) != false -> "생년월일을 확인해 주세요"
            else -> null
        }
    }
}
