package com.example.pickup_hyundai.domain

import com.example.pickup_hyundai.domain.model.LinkDelivery
import java.time.Duration
import java.time.LocalDateTime

/** 08_USER_FLOW §4-5 정차시간: 잔여 >30초 일반, ≤30초 경고, ≤0 초과 */
object StopRules {

    enum class TimerPhase { NORMAL, WARNING, OVER }

    fun remainingSec(now: LocalDateTime, startedAt: LocalDateTime, allowedSec: Int): Long =
        (allowedSec - Duration.between(startedAt, now).seconds).coerceAtLeast(0)

    fun phase(remainingSec: Long): TimerPhase = when {
        remainingSec <= 0 -> TimerPhase.OVER
        remainingSec <= 30 -> TimerPhase.WARNING
        else -> TimerPhase.NORMAL
    }

    enum class ChildStatus { LEAVING, OPENED, NOT_OPENED, NOT_SENT }

    fun childStatus(delivery: LinkDelivery): ChildStatus = when {
        delivery.leavingAt != null -> ChildStatus.LEAVING
        delivery.openedAt != null -> ChildStatus.OPENED
        delivery.sentAt != null -> ChildStatus.NOT_OPENED
        else -> ChildStatus.NOT_SENT
    }
}
