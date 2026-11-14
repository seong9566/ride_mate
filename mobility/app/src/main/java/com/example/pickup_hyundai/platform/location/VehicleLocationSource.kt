package com.example.pickup_hyundai.platform.location

import ai.pleos.playground.location.fused.FusedLocation
import ai.pleos.playground.location.fused.listener.FusedLocationListener
import ai.pleos.playground.location.fused.model.LocationData
import android.annotation.SuppressLint
import android.content.Context
import android.util.Log
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update

data class VehicleLocation(
    val latitude: Double,
    val longitude: Double,
    val heading: Float,
    val timestampMillis: Long,
)

data class LocationUiState(
    val supported: Boolean? = null,
    val location: VehicleLocation? = null,
    val lastError: String? = null,
)

/**
 * Pleos FusedLocation SDK(1.1.3) 래퍼.
 * 권한: pleos.car.permission.FUSED_LOCATION (protection level: dangerous → 런타임 요청 필요)
 * 근거: FusedLocation-1.1.3.aar 클래스 시그니처, 공식 문서 location-data-access
 */
@SuppressLint("MissingPermission")
class VehicleLocationSource(context: Context) {

    private val fused = FusedLocation(context.applicationContext)
    private val _state = MutableStateFlow(LocationUiState())
    val state: StateFlow<LocationUiState> = _state.asStateFlow()
    private var started = false

    private val listener = object : FusedLocationListener {
        override fun onChanged(location: LocationData?) {
            // null 위치는 "모름"으로 처리하고 이전 위치를 실시간처럼 유지하지 않는다
            _state.update { it.copy(location = location?.toModel()) }
        }

        override fun onError(exception: Exception) {
            Log.e(TAG, "FusedLocation error", exception)
            _state.update { it.copy(lastError = exception.message) }
        }
    }

    /** FUSED_LOCATION 런타임 권한이 허용된 뒤 호출한다. */
    fun start() {
        if (started) return
        val supported = runCatching { fused.isSupported() }.getOrDefault(false)
        _state.update { it.copy(supported = supported) }
        if (!supported) return
        started = true
        fused.initialize()
        fused.getLastKnownLocation(listener)
        fused.registerFusedLocationCallback(listener)
    }

    fun stop() {
        if (!started) return
        started = false
        fused.unregisterFusedLocationCallback(listener)
        fused.release()
    }

    private fun LocationData.toModel() = VehicleLocation(latitude, longitude, heading, timestampMillis)

    private companion object { const val TAG = "WayMe/Location" }
}
