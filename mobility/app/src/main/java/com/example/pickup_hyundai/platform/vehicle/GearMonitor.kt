package com.example.pickup_hyundai.platform.vehicle

import ai.pleos.playground.vehicle.Vehicle
import ai.pleos.playground.vehicle.constant.control.VehicleGear
import ai.pleos.playground.vehicle.listener.GearSelectionListener
import android.annotation.SuppressLint
import android.content.Context
import android.util.Log
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow

/**
 * 기어 상태(Vehicle SDK 2.3.2 Gear API). 디자인 V05 "정차(P) 중에만 누를 수 있어요" 판정에 사용.
 * 권한: pleos.car.permission.CAR_DRIVE (App Market 사전 승인 필요)
 * 근거: Vehicle-2.3.2-sources api/Gear.kt, listener/GearSelectionListener.kt
 */
@SuppressLint("MissingPermission")
class GearMonitor(context: Context) {

    private val vehicle = Vehicle(context.applicationContext)
    private val _gear = MutableStateFlow<VehicleGear?>(null)
    /** null = 아직 모름(조회 전·실패) */
    val gear: StateFlow<VehicleGear?> = _gear.asStateFlow()
    private var started = false

    private val listener = object : GearSelectionListener {
        override fun onSelected(gear: VehicleGear?) {
            Log.d(TAG, "gear=$gear")
            _gear.value = gear
        }

        override fun onFailed(e: Exception) {
            Log.e(TAG, "gear listener failed", e)
        }
    }

    fun start() {
        if (started) return
        started = true
        try {
            vehicle.initialize()
            val api = vehicle.getGear()
            api.getCurrentGearSelectionState(
                onComplete = { g: VehicleGear? -> _gear.value = g },
                onFailed = { e: Exception -> Log.e(TAG, "gear get failed", e) },
            )
            api.registerGearSelection(listener)
        } catch (e: Exception) {
            Log.e(TAG, "gear start failed (permissions or service missing)", e)
        }
    }

    fun stop() {
        if (!started) return
        started = false
        vehicle.getGear().unregisterGearSelection(listener)
        vehicle.release()
    }

    private companion object { const val TAG = "WayMe/Gear" }
}

/** P단으로 확인된 경우에만 true. 모르는 상태(null)는 안전하게 false로 본다. */
fun VehicleGear?.isParked(): Boolean = this == VehicleGear.GEAR_PARK
