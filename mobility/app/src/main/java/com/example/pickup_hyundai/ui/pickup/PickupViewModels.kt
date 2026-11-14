package com.example.pickup_hyundai.ui.pickup

import androidx.lifecycle.ViewModel
import androidx.lifecycle.ViewModelProvider
import androidx.lifecycle.viewModelScope
import com.example.pickup_hyundai.data.repository.PickupRepository
import com.example.pickup_hyundai.domain.model.DeparturePlan
import com.example.pickup_hyundai.domain.model.PickupSpotChoice
import com.example.pickup_hyundai.domain.model.WaitingPlaceChoice
import com.example.pickup_hyundai.domain.model.TodayPickupDetail
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch

sealed interface LoadState<out T> {
    data object Loading : LoadState<Nothing>
    data class Error(val message: String) : LoadState<Nothing>
    data class Content<T>(val data: T) : LoadState<T>
}

/** V03 오늘 하원 일정 */
class TodayPickupViewModel(private val repository: PickupRepository) : ViewModel() {
    private val _state = MutableStateFlow<LoadState<TodayPickupDetail>>(LoadState.Loading)
    val state: StateFlow<LoadState<TodayPickupDetail>> = _state.asStateFlow()

    init { refresh() }

    fun refresh() {
        _state.value = LoadState.Loading
        viewModelScope.launch {
            _state.value = repository.getTodayPickup().fold(
                { LoadState.Content(it) },
                { LoadState.Error(it.message ?: "오늘 일정을 불러오지 못했어요") },
            )
        }
    }
}

data class PickupSpotUiState(
    val load: LoadState<PickupSpotChoice> = LoadState.Loading,
    /** 선택한 후보 rank. 디자인상 처음엔 선택 없음 → 카드 탭 시 강조 */
    val selectedRank: Int? = null,
)

/** V04 픽업 장소 추천 */
class PickupSpotViewModel(private val repository: PickupRepository) : ViewModel() {
    private val _state = MutableStateFlow(PickupSpotUiState())
    val state: StateFlow<PickupSpotUiState> = _state.asStateFlow()

    init { refresh() }

    fun refresh() {
        _state.update { it.copy(load = LoadState.Loading) }
        viewModelScope.launch {
            val load = repository.getPickupSpotChoice().fold(
                { LoadState.Content(it) },
                { LoadState.Error(it.message ?: "추천 장소를 불러오지 못했어요") },
            )
            _state.update { it.copy(load = load) }
        }
    }

    fun select(rank: Int) = _state.update { it.copy(selectedRank = rank) }
}

/** V05 출발 전 */
class DepartureViewModel(private val repository: PickupRepository) : ViewModel() {
    private val _state = MutableStateFlow<LoadState<DeparturePlan>>(LoadState.Loading)
    val state: StateFlow<LoadState<DeparturePlan>> = _state.asStateFlow()

    init { refresh() }

    fun refresh() {
        _state.value = LoadState.Loading
        viewModelScope.launch {
            _state.value = repository.getDeparturePlan().fold(
                { LoadState.Content(it) },
                { LoadState.Error(it.message ?: "출발 정보를 불러오지 못했어요") },
            )
        }
    }

    /** 정본 B-1: 출발 30분 전부터 화면이 떠 있는 동안 1분 간격 재조회. 실패하면 기존 화면 유지 */
    fun refreshQuietly() {
        viewModelScope.launch {
            repository.getDeparturePlan().onSuccess { _state.value = LoadState.Content(it) }
        }
    }
}

data class WaitingPlaceUiState(
    val load: LoadState<WaitingPlaceChoice> = LoadState.Loading,
    /** 디자인상 추천(1번)이 기본 선택 */
    val selectedRank: Int? = null,
)

/** V06 대기 장소 추천 */
class WaitingPlaceViewModel(private val repository: PickupRepository) : ViewModel() {
    private val _state = MutableStateFlow(WaitingPlaceUiState())
    val state: StateFlow<WaitingPlaceUiState> = _state.asStateFlow()

    init { refresh() }

    fun refresh() {
        _state.update { it.copy(load = LoadState.Loading) }
        viewModelScope.launch {
            repository.getWaitingPlaces().fold(
                { choice ->
                    val default = choice.places.firstOrNull { it.recommended }?.rank ?: choice.places.firstOrNull()?.rank
                    _state.update { it.copy(load = LoadState.Content(choice), selectedRank = it.selectedRank ?: default) }
                },
                { e -> _state.update { it.copy(load = LoadState.Error(e.message ?: "대기 장소를 불러오지 못했어요")) } },
            )
        }
    }

    fun select(rank: Int) = _state.update { it.copy(selectedRank = rank) }
}

class PickupViewModelFactory(private val repository: PickupRepository) : ViewModelProvider.Factory {
    @Suppress("UNCHECKED_CAST")
    override fun <T : ViewModel> create(modelClass: Class<T>): T = when (modelClass) {
        TodayPickupViewModel::class.java -> TodayPickupViewModel(repository)
        PickupSpotViewModel::class.java -> PickupSpotViewModel(repository)
        DepartureViewModel::class.java -> DepartureViewModel(repository)
        WaitingPlaceViewModel::class.java -> WaitingPlaceViewModel(repository)
        else -> error("Unknown ViewModel $modelClass")
    } as T
}
