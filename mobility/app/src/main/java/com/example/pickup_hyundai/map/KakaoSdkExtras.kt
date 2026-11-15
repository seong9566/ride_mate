package com.example.pickup_hyundai.map

import com.kakao.vectormap.KakaoMap
import com.kakao.vectormap.LatLng
import com.kakao.vectormap.MapView
import com.kakao.vectormap.label.Label

/**
 * 공식 문서에 예시가 없는 카카오맵 SDK 동작(라벨 이동·삭제, 레이어 비우기, MapView 종료)을 한곳에 모은다.
 * tools/verify_kakao_sdk.py로 SDK 2.15.2 클래스에서 존재 확인 완료(2026-11-14):
 * Label.moveTo(LatLng), LabelLayer.remove(Label...), LabelLayer/ShapeLayer/RouteLineLayer.removeAll(), MapView.finish().
 * SDK 버전을 올리면 다시 확인한다.
 */
internal object KakaoSdkExtras {
    /** 라벨 위치 이동. 성공하면 true, 지원하지 않으면 false(호출부가 삭제 후 재생성) */
    fun move(label: Label, pos: LatLng): Boolean = runCatching { label.moveTo(pos); true }.getOrDefault(false)

    fun remove(map: KakaoMap, label: Label) {
        runCatching { map.labelManager?.layer?.remove(label) }
    }

    fun clear(map: KakaoMap) {
        runCatching { map.labelManager?.layer?.removeAll() }
        runCatching { map.shapeManager?.layer?.removeAll() }
        runCatching { map.routeLineManager?.layer?.removeAll() }
    }

    fun finish(view: MapView?) {
        runCatching { view?.finish() }
    }
}
