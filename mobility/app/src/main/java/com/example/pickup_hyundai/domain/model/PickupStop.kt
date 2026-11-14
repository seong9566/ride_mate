package com.example.pickup_hyundai.domain.model

import java.time.LocalDateTime

/** 자녀 링크 전달 단계(디자인 "전달 상태": 보냄 → 열람 → 지금 나가요) */
enum class DeliveryStep { SENT, OPENED, LEAVING }

/**
 * 자녀 링크 전달 상태.
 * 정본: 자녀 웹 API(AddPickupLinkOpen, AddChildLeave)가 이 값을 만들지만, 차량 앱이 읽는 API는 정본에 없음 → Mock.
 */
data class LinkDelivery(
    val sentAt: LocalDateTime?,
    val openedAt: LocalDateTime?,
    val leavingAt: LocalDateTime?,
) {
    val reached: DeliveryStep?
        get() = when {
            leavingAt != null -> DeliveryStep.LEAVING
            openedAt != null -> DeliveryStep.OPENED
            sentAt != null -> DeliveryStep.SENT
            else -> null
        }

    fun isDone(step: DeliveryStep): Boolean = reached?.let { it.ordinal >= step.ordinal } ?: false
}

/** V09 아이에게 픽업 장소 보내기 */
data class SendLinkPreview(
    val childName: String,
    /** 문자 본문 미리보기. 실제 문구는 발송 API(정본 없음) 기준으로 확정 */
    val messageText: String,
    val delivery: LinkDelivery,
    val isMock: Boolean,
)

/** V09 픽업 정차 */
data class PickupStopInfo(
    val childName: String,
    /** 정본: DetailDepartureGuide.enforcement.status == RESTRICTED */
    val inRestrictedZone: Boolean,
    val restrictedMessage: String?,
    /** 정차 허용 시간(초). 08_USER_FLOW §4-5: 근거가 확보된 장소에서만 사용 → 정본 없음, Mock */
    val allowedStopSec: Int?,
    val stopStartedAt: LocalDateTime,
    val delivery: LinkDelivery,
    val isMock: Boolean,
)
