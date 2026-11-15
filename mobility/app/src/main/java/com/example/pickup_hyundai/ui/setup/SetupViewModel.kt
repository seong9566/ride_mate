package com.example.pickup_hyundai.ui.setup

import androidx.lifecycle.ViewModel
import androidx.lifecycle.ViewModelProvider
import androidx.lifecycle.viewModelScope
import com.example.pickup_hyundai.data.repository.SetupRepository
import com.example.pickup_hyundai.domain.model.AcademyInput
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

enum class SetupEvent { CHILD_SAVED, ACADEMY_SAVED, HOME_SAVED }

data class SetupUiState(
    val child: ChildInfoInput = ChildInfoInput(),
    val childError: String? = null,
    val schedules: List<AcademySchedule> = emptyList(),
    val selectedDay: DayOfWeek = LocalDate.now().dayOfWeek,
    /** 학원 추가 화면 입력값 */
    val academy: AcademyInput = AcademyInput(),
    val academyError: String? = null,
    val naviHome: HomeAddress? = null,
    val homeConfirmed: Boolean = false,
    /** 내비 '집'이 없거나 다른 곳으로 할 때: 주소 직접 입력 + 지금 차 위치로 등록 */
    val manualHome: Boolean = false,
    val manualAddress: String = "",
    val summary: SetupSummary? = null,
    val busy: Boolean = false,
    val error: String? = null,
    val event: SetupEvent? = null,
    val isMock: Boolean = false,
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
        _state.update { state ->
            val child = transform(state.child)
            if (child == state.child) state else state.copy(child = child, childError = null)
        }

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
        // 직접 등록 중이거나 이미 확인한 집은 덮어쓰지 않는다
        if (_state.value.manualHome || _state.value.homeConfirmed) return
        viewModelScope.launch { repository.getNaviHome().onSuccess { h -> _state.update { it.copy(naviHome = h) } } }
    }

    // ───────── 학원 추가 ─────────

    /** 추가 화면을 열 때: 지금 보고 있는 요일을 미리 고른 새 입력으로 시작 */
    fun startAcademy() = _state.update { it.copy(academy = AcademyInput(days = setOf(it.selectedDay)), academyError = null) }

    fun updateAcademy(transform: (AcademyInput) -> AcademyInput) =
        _state.update { it.copy(academy = transform(it.academy), academyError = null) }

    fun toggleAcademyDay(day: DayOfWeek) = updateAcademy { a -> a.copy(days = if (day in a.days) a.days - day else a.days + day) }

    /** 하원 시각 조정(시 ±1, 분 ±5). 서버 규칙: 14:00~23:30, 5분 단위 */
    fun adjustAcademyTime(hours: Int, minutes: Int) = updateAcademy { a ->
        val total = (a.endTime.hour * 60 + a.endTime.minute + hours * 60 + minutes).coerceIn(14 * 60, 23 * 60 + 30)
        a.copy(endTime = java.time.LocalTime.of(total / 60, total % 60))
    }

    /** 학원 위치를 지금 차 위치로(장소 검색 연결 전 임시 수단). 좌표가 없으면 서버가 픽업 장소를 배정하지 못한다 */
    fun setAcademyLocation(lat: Double, lng: Double) = updateAcademy { it.copy(latitude = lat, longitude = lng) }

    fun saveAcademy() {
        val a = _state.value.academy
        val error = when {
            a.displayName.isBlank() -> "학원 이름을 입력해 주세요"
            !NAME_RULE.matches(a.displayName.trim()) -> "학원 이름은 한글·영문·숫자와 ( ) - · 만 쓸 수 있어요(20자까지)"
            a.placeName.isBlank() -> "학원 장소(상호)를 입력해 주세요"
            a.address.isBlank() -> "학원 주소를 입력해 주세요"
            a.days.isEmpty() -> "요일을 하나 이상 골라 주세요"
            else -> null
        }
        if (error != null) { _state.update { it.copy(academyError = error) }; return }
        if (_state.value.busy) return
        _state.update { it.copy(busy = true, academyError = null) }
        viewModelScope.launch {
            repository.addAcademy(a).fold(
                { notice ->
                    val list = repository.getSchedules().getOrDefault(_state.value.schedules)
                    _state.update { it.copy(busy = false, schedules = list, selectedDay = a.days.minOrNull() ?: it.selectedDay, error = notice, event = SetupEvent.ACADEMY_SAVED) }
                },
                { t -> _state.update { it.copy(busy = false, academyError = t.message ?: "저장하지 못했어요") } },
            )
        }
    }

    fun startManualHome() = _state.update { it.copy(manualHome = true, homeConfirmed = false) }

    fun updateManualAddress(v: String) = _state.update { it.copy(manualAddress = v.take(60)) }

    /** 지금 차 위치(FusedLocation)를 집으로 등록. 주소 검색 API가 없어 좌표는 현재 위치를 쓴다 */
    fun useCurrentLocationAsHome(lat: Double, lng: Double) = _state.update {
        it.copy(
            naviHome = HomeAddress(it.manualAddress.trim().ifBlank { "지금 차 위치" }, lat, lng, fromNaviBookmark = false),
            homeConfirmed = true,
            error = null,
        )
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
        /** AddSchedule displayName 규칙: 한글·영문·숫자·공백과 ( ) - · , 1~20자 */
        private val NAME_RULE = Regex("^[가-힣ㄱ-ㅎㅏ-ㅣa-zA-Z0-9 ()\\-·]{1,20}$")
        /** AddChild childName 규칙: 완성형 한글·영문, 단어 사이 공백 한 칸까지, 1~10자 */
        private val CHILD_NAME_RULE = Regex("^(?=.{1,10}$)[가-힣a-zA-Z]+( [가-힣a-zA-Z]+)*$")
        private val BIRTH = DateTimeFormatter.ofPattern("uuuuMMdd").withResolverStyle(ResolverStyle.STRICT)

        /** 입력 검증. 정상이면 null */
        fun validateChild(c: ChildInfoInput, today: LocalDate = LocalDate.now()): String? = when {
            c.name.isBlank() -> "이름을 입력해 주세요"
            !CHILD_NAME_RULE.matches(c.name.trim()) -> "이름은 한글·영문만 10자까지 쓸 수 있어요"
            !Regex("^01[016789][0-9]{7,8}$").matches(c.phoneDigits) -> "휴대폰 번호 11자리를 입력해 주세요"
            c.birthDigits.length != 8 -> "생년월일 8자리를 입력해 주세요(예: 20100514)"
            runCatching { LocalDate.parse(c.birthDigits, BIRTH) }.getOrNull()?.isAfter(today) != false -> "생년월일을 확인해 주세요"
            else -> null
        }
    }
}
