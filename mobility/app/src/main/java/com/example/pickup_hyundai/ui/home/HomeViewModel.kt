package com.example.pickup_hyundai.ui.home

import androidx.lifecycle.ViewModel
import androidx.lifecycle.ViewModelProvider
import androidx.lifecycle.viewModelScope
import com.example.pickup_hyundai.data.repository.HomeRepository
import com.example.pickup_hyundai.domain.model.HomeSummary
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch

sealed interface HomeUiState {
    data object Loading : HomeUiState
    data class Error(val message: String) : HomeUiState
    data class Content(val summary: HomeSummary) : HomeUiState
}

class HomeViewModel(private val repository: HomeRepository) : ViewModel() {

    private val _state = MutableStateFlow<HomeUiState>(HomeUiState.Loading)
    val state: StateFlow<HomeUiState> = _state.asStateFlow()

    init { refresh() }

    fun refresh() {
        _state.value = HomeUiState.Loading
        viewModelScope.launch {
            _state.value = repository.getHomeSummary().fold(
                onSuccess = { HomeUiState.Content(it) },
                onFailure = { HomeUiState.Error(it.message ?: "일정을 불러오지 못했어요") },
            )
        }
    }

    class Factory(private val repository: HomeRepository) : ViewModelProvider.Factory {
        @Suppress("UNCHECKED_CAST")
        override fun <T : ViewModel> create(modelClass: Class<T>): T = HomeViewModel(repository) as T
    }
}
