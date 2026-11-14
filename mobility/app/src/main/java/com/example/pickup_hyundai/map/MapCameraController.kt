package com.example.pickup_hyundai.map

import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import com.example.pickup_hyundai.domain.route.GeoPoint
import com.kakao.vectormap.KakaoMap
import com.kakao.vectormap.LatLng
import com.kakao.vectormap.camera.CameraAnimation
import com.kakao.vectormap.camera.CameraUpdateFactory

/**
 * 지도 우측 상단 버튼(2D/3D, 현재 위치) 동작.
 * SDK 2.15.2 클래스에서 확인한 API만 사용: CameraUpdateFactory.newCenterPosition(LatLng), tiltTo(double), CameraAnimation.from(int)
 * tiltTo 단위는 라디안(카카오맵 v2 CameraPosition 기준) — 에뮬레이터에서 기울기 확인 필요.
 */
class MapCameraController {
    internal var map: KakaoMap? = null
    var tilted by mutableStateOf(false)
        private set

    fun recenter(target: GeoPoint?) {
        val t = target ?: return
        runCatching { map?.moveCamera(CameraUpdateFactory.newCenterPosition(LatLng.from(t.lat, t.lng)), CameraAnimation.from(300)) }
    }

    fun toggleTilt() {
        tilted = !tilted
        runCatching { map?.moveCamera(CameraUpdateFactory.tiltTo(if (tilted) Math.toRadians(45.0) else 0.0), CameraAnimation.from(300)) }
    }
}
