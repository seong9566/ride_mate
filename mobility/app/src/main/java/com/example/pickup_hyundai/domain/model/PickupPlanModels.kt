package com.example.pickup_hyundai.domain.model

import java.time.LocalDateTime

/** 오늘 확정된 픽업 계획(정본 AddPickupPlan 응답 요약) */
data class AssignedPlan(
    /** 「경로 시작」에 필요한 계획 ID. Mock이면 null */
    val pickupPlanSeq: Long?,
    val pickupName: String?,
    val latitude: Double?,
    val longitude: Double?,
    val walkMin: Int?,
    /** 배정 도착 시각(같은 학원가 부모끼리 시간 분산) */
    val slotDt: LocalDateTime?,
    val guideText: String?,
    val isMock: Boolean,
)
