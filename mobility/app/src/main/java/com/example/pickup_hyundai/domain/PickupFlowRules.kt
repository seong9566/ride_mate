package com.example.pickup_hyundai.domain

import java.time.Duration
import java.time.LocalDateTime

/** 08_USER_FLOW §4 시간 경계 규칙 */
object PickupFlowRules {

    enum class AfterSpotSelected { WAITING_PLACE /* V06 */, BEFORE_DEPARTURE /* V05 */ }

    /** V06 대기 장소 추천 기준(분). 디자인 "출발추천시각이 10분이상남았을경우" 기준(2026-11-14 결정, USER_FLOW §4-2의 30분에서 변경) */
    const val WAITING_PLACE_MIN_LEAD_MIN = 10L

    /** departureDt − now ≥ 10분 → V06(대기 장소), < 10분(지남 포함) → V05(출발 전) */
    fun afterSpotSelected(now: LocalDateTime, departureDt: LocalDateTime): AfterSpotSelected =
        if (Duration.between(now, departureDt) >= Duration.ofMinutes(WAITING_PLACE_MIN_LEAD_MIN)) AfterSpotSelected.WAITING_PLACE
        else AfterSpotSelected.BEFORE_DEPARTURE
}
