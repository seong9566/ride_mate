package com.example.pickup_hyundai.ui.pickup

import androidx.lifecycle.ViewModel
import androidx.lifecycle.ViewModelProvider
import androidx.lifecycle.viewModelScope
import com.example.pickup_hyundai.data.local.LocalRecordStore
import com.example.pickup_hyundai.data.repository.DriveRepository
import com.example.pickup_hyundai.domain.model.HomeDrive
import com.example.pickup_hyundai.domain.model.PickupRecord
import com.example.pickup_hyundai.domain.model.WaitingDrive
import com.example.pickup_hyundai.domain.model.WaitingPlace
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext

/** V07 대기 장소로 이동 */
class WaitingDriveViewModel(private val repository: DriveRepository) : ViewModel() {
    private val _state = MutableStateFlow<LoadState<WaitingDrive>>(LoadState.Loading)
    val state: StateFlow<LoadState<WaitingDrive>> = _state.asStateFlow()
    private var place: WaitingPlace? = null

    fun load(place: WaitingPlace) {
        if (this.place == place && _state.value is LoadState.Content) return
        this.place = place
        _state.value = LoadState.Loading
        viewModelScope.launch {
            _state.value = repository.getWaitingDrive(place).fold(
                { LoadState.Content(it) },
                { LoadState.Error(it.message ?: "안내 정보를 불러오지 못했어요") },
            )
        }
    }

    fun retry() { place?.let { this.place = null; load(it) } }
}

/** V10 집으로 가는 중 */
class HomeDriveViewModel(private val repository: DriveRepository) : ViewModel() {
    private val _state = MutableStateFlow<LoadState<HomeDrive>>(LoadState.Loading)
    val state: StateFlow<LoadState<HomeDrive>> = _state.asStateFlow()

    init { refresh() }

    fun refresh() {
        _state.value = LoadState.Loading
        viewModelScope.launch {
            _state.value = repository.getHomeDrive().fold(
                { LoadState.Content(it) },
                { LoadState.Error(it.message ?: "안내 정보를 불러오지 못했어요") },
            )
        }
    }
}

data class RecordUiState(
    val load: LoadState<PickupRecord> = LoadState.Loading,
    val autoSave: Boolean = false,
    val saving: Boolean = false,
    /** 저장/건너뛰기 완료 이벤트(1회 소비) */
    val doneEvent: Boolean = false,
    val error: String? = null,
    /** "다음부터 묻지 않고 저장"이 켜져 있어 화면 진입 시 자동 저장됨 */
    val autoSaved: Boolean = false,
)

/** V11 픽업 완료·기록 저장 */
class RecordViewModel(
    private val repository: DriveRepository,
    private val store: LocalRecordStore,
) : ViewModel() {
    private val _state = MutableStateFlow(RecordUiState(autoSave = store.autoSave))
    val state: StateFlow<RecordUiState> = _state.asStateFlow()

    init { refresh() }

    fun refresh() {
        _state.update { it.copy(load = LoadState.Loading) }
        viewModelScope.launch {
            val load = repository.getTodayRecord().fold(
                { LoadState.Content(it) },
                { LoadState.Error(it.message ?: "오늘 기록을 불러오지 못했어요") },
            )
            _state.update { it.copy(load = load) }
            if (load is LoadState.Content && store.autoSave) {
                val ok = runCatching { withContext(Dispatchers.IO) { store.save(load.data) } }.isSuccess
                _state.update { it.copy(autoSaved = ok) }
            }
        }
    }

    fun setAutoSave(enabled: Boolean) = _state.update { it.copy(autoSave = enabled) }

    fun save() {
        val record = (_state.value.load as? LoadState.Content)?.data ?: return
        if (_state.value.saving) return
        _state.update { it.copy(saving = true, error = null) }
        viewModelScope.launch {
            val result = runCatching {
                withContext(Dispatchers.IO) {
                    store.autoSave = _state.value.autoSave
                    store.save(record)
                }
            }
            _state.update {
                if (result.isSuccess) it.copy(saving = false, doneEvent = true)
                else it.copy(saving = false, error = "저장하지 못했어요")
            }
        }
    }

    fun skip() = _state.update { it.copy(doneEvent = true) }

    fun consumeDone() = _state.update { it.copy(doneEvent = false) }
}

class DriveViewModelFactory(
    private val repository: DriveRepository,
    private val store: LocalRecordStore,
) : ViewModelProvider.Factory {
    @Suppress("UNCHECKED_CAST")
    override fun <T : ViewModel> create(modelClass: Class<T>): T = when (modelClass) {
        WaitingDriveViewModel::class.java -> WaitingDriveViewModel(repository)
        HomeDriveViewModel::class.java -> HomeDriveViewModel(repository)
        RecordViewModel::class.java -> RecordViewModel(repository, store)
        else -> error("Unknown ViewModel $modelClass")
    } as T
}
