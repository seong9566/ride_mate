package com.example.pickup_hyundai.platform.vehicle

import ai.pleos.playground.vehicle.Vehicle
import ai.pleos.playground.vehicle.api.model.area.DoorArea
import ai.pleos.playground.vehicle.api.model.capability.AreaBounds
import ai.pleos.playground.vehicle.api.model.capability.CapabilityConfig
import ai.pleos.playground.vehicle.constant.area.DoorValue
import ai.pleos.playground.vehicle.listener.DoorPositionListener
import android.content.Context
import android.util.Log
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update

enum class RearDoor(val value: DoorValue) {
    LEFT(DoorValue.ROW_2_LEFT),
    RIGHT(DoorValue.ROW_2_RIGHT),
}

data class RearDoorState(
    /** Vehicle Service가 보고한 원시 위치값(null = 아직 모름) */
    val position: Int? = null,
    /** checkDoorPositionCapabilities가 알려준 최소값(닫힘). 미확인 시 null */
    val closedValue: Int? = null,
    val supported: Boolean? = null,
) {
    /** 최소값(닫힘)보다 크면 열림. 범위를 0/1로 가정하지 않는다. */
    val isOpen: Boolean? get() = position?.let { it > (closedValue ?: 0) }
}

data class RearDoorUiState(
    val doors: Map<RearDoor, RearDoorState> = RearDoor.entries.associateWith { RearDoorState() },
    val boardingCandidateCount: Int = 0,
    val lastError: String? = null,
)

/**
 * Pleos Vehicle SDK(2.3.2) 뒷문 위치 래퍼.
 *
 * - SDK는 initialize() 직후의 호출을 연결 완료까지 대기열에 보관하고, 재연결 시 리스너를 재등록한다
 *   (Vehicle-2.3.2-sources: BaseServiceConnector.cachedCall / reConnectedWithService).
 * - 권한: pleos.car.permission.CAR_DOORS
 */
class RearDoorMonitor(context: Context) {

    private val vehicle = Vehicle(context.applicationContext)
    private val detector = BoardingSignalDetector()
    private val _state = MutableStateFlow(RearDoorUiState())
    val state: StateFlow<RearDoorUiState> = _state.asStateFlow()

    private val listeners = RearDoor.entries.associateWith { door ->
        object : DoorPositionListener {
            override fun onPositionUpdated(pos: Int?) = applyPosition(door, pos)
            override fun onFailed(e: Exception) = onError("door ${door.name}", e)
        }
    }
    private var started = false

    fun start() {
        if (started) return
        started = true
        vehicle.initialize()
        val doorApi = vehicle.getDoor()

        doorApi.checkDoorPositionCapabilities(
            { _: CapabilityConfig, bounds: Map<DoorArea, AreaBounds<Int>> ->
                RearDoor.entries.forEach { door ->
                    val b = bounds[DoorArea(door.value)]
                    updateDoor(door) { it.copy(supported = b != null, closedValue = b?.minValue) }
                }
            },
            { e: Exception -> onError("capability", e) },
        )
        RearDoor.entries.forEach { door ->
            val area = DoorArea(door.value)
            doorApi.getCurrentDoorPosition(
                area = area,
                onComplete = { pos: Int? -> applyPosition(door, pos) },
                onFailed = { e: Exception -> onError("get ${door.name}", e) },
            )
            doorApi.registerDoorPosition(area, listeners.getValue(door))
        }
    }

    fun stop() {
        if (!started) return
        started = false
        val doorApi = vehicle.getDoor()
        RearDoor.entries.forEach { door ->
            doorApi.unregisterDoorPosition(DoorArea(door.value), listeners.getValue(door))
        }
        vehicle.release()
    }

    private fun applyPosition(door: RearDoor, pos: Int?) {
        Log.d(TAG, "door ${door.name} position=$pos")
        updateDoor(door) { it.copy(position = pos) }
        val isOpen = _state.value.doors.getValue(door).isOpen ?: return
        if (detector.onDoorChanged(door, isOpen)) {
            _state.update { it.copy(boardingCandidateCount = it.boardingCandidateCount + 1) }
        }
    }

    private fun updateDoor(door: RearDoor, transform: (RearDoorState) -> RearDoorState) {
        _state.update { s -> s.copy(doors = s.doors + (door to transform(s.doors.getValue(door)))) }
    }

    private fun onError(where: String, e: Exception) {
        Log.e(TAG, "Vehicle SDK error ($where)", e)
        _state.update { it.copy(lastError = "$where: ${e.message}") }
    }

    private companion object { const val TAG = "WayMe/RearDoor" }
}
