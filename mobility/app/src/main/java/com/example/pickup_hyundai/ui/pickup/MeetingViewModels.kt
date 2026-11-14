package com.example.pickup_hyundai.ui.pickup

import androidx.lifecycle.ViewModel
import androidx.lifecycle.ViewModelProvider
import androidx.lifecycle.viewModelScope
import com.example.pickup_hyundai.data.repository.MeetingRepository
import com.example.pickup_hyundai.domain.model.PickupStopInfo
import com.example.pickup_hyundai.domain.model.SendLinkPreview
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch

data class SendLinkUiState(
    val load: LoadState<SendLinkPreview> = LoadState.Loading,
    val sending: Boolean = false,
    val sendError: String? = null,
    /** 발송 성공 이벤트(1회 소비) */
    val sentEvent: Boolean = false,
)

class SendLinkViewModel(private val repository: MeetingRepository) : ViewModel() {
    private val _state = MutableStateFlow(SendLinkUiState())
    val state: StateFlow<SendLinkUiState> = _state.asStateFlow()

    init { refresh() }

    fun refresh() {
        _state.update { it.copy(load = LoadState.Loading) }
        viewModelScope.launch {
            val load = repository.getSendLinkPreview().fold(
                { LoadState.Content(it) },
                { LoadState.Error(it.message ?: "메시지를 준비하지 못했어요") },
            )
            _state.update { it.copy(load = load) }
        }
    }

    fun send() {
        if (_state.value.sending) return // 중복 발송 방지(§4-3)
        _state.update { it.copy(sending = true, sendError = null) }
        viewModelScope.launch {
            repository.sendLink().fold(
                { delivery ->
                    _state.update { s ->
                        val load = (s.load as? LoadState.Content)?.let { LoadState.Content(it.data.copy(delivery = delivery)) } ?: s.load
                        s.copy(load = load, sending = false, sentEvent = true)
                    }
                },
                { e -> _state.update { it.copy(sending = false, sendError = e.message ?: "보내지 못했어요. 다시 시도해 주세요") } },
            )
        }
    }

    fun consumeSentEvent() = _state.update { it.copy(sentEvent = false) }
}

data class PickupStopUiState(
    val load: LoadState<PickupStopInfo> = LoadState.Loading,
    val reminding: Boolean = false,
    val message: String? = null,
)

class PickupStopViewModel(private val repository: MeetingRepository) : ViewModel() {
    private val _state = MutableStateFlow(PickupStopUiState())
    val state: StateFlow<PickupStopUiState> = _state.asStateFlow()

    init { refresh() }

    fun refresh() {
        _state.update { it.copy(load = LoadState.Loading) }
        viewModelScope.launch {
            val load = repository.getPickupStop().fold(
                { LoadState.Content(it) },
                { LoadState.Error(it.message ?: "정차 정보를 불러오지 못했어요") },
            )
            _state.update { it.copy(load = load) }
        }
    }

    fun remindArrived() {
        if (_state.value.reminding) return
        _state.update { it.copy(reminding = true, message = null) }
        viewModelScope.launch {
            val msg = repository.remindArrived().fold({ "아이에게 다시 알렸어요" }, { it.message ?: "알리지 못했어요" })
            _state.update { it.copy(reminding = false, message = msg) }
        }
    }

    fun consumeMessage() = _state.update { it.copy(message = null) }
}

class MeetingViewModelFactory(private val repository: MeetingRepository) : ViewModelProvider.Factory {
    @Suppress("UNCHECKED_CAST")
    override fun <T : ViewModel> create(modelClass: Class<T>): T = when (modelClass) {
        SendLinkViewModel::class.java -> SendLinkViewModel(repository)
        PickupStopViewModel::class.java -> PickupStopViewModel(repository)
        else -> error("Unknown ViewModel $modelClass")
    } as T
}
