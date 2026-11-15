package com.example.pickup_hyundai.platform.vehicle

/**
 * 뒷문 열림 → 닫힘 전이를 "탑승 후보" 신호로 판정한다. (순수 로직, SDK 의존 없음)
 *
 * 문 신호만으로 탑승 완료를 확정하지 않는다(08_USER_FLOW §4-7).
 * 이 결과는 운전자 확인 버튼을 띄우는 트리거로만 사용한다.
 */
class BoardingSignalDetector {

    private val wasOpened = mutableSetOf<RearDoor>()

    /** 문 상태가 바뀔 때 호출. 열렸다가 닫힌 문이면 true(탑승 후보). */
    fun onDoorChanged(door: RearDoor, isOpen: Boolean): Boolean {
        if (isOpen) {
            wasOpened += door
            return false
        }
        return wasOpened.remove(door)
    }

    fun reset() = wasOpened.clear()
}
