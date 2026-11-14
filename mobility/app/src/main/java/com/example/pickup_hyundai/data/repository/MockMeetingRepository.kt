package com.example.pickup_hyundai.data.repository

import com.example.pickup_hyundai.domain.model.LinkDelivery
import com.example.pickup_hyundai.domain.model.PickupStopInfo
import com.example.pickup_hyundai.domain.model.SendLinkPreview
import java.time.Clock
import java.time.LocalDateTime
import java.time.ZoneId

/**
 * 디자인 예시 값 Mock.
 * @param childScenario 정차 화면의 아이 상태 시나리오(LEAVING: 지금 나가요 / NOT_OPENED: 아이 미확인)
 */
class MockMeetingRepository(
    private val childScenario: String = "LEAVING",
    private val clock: Clock = Clock.system(ZoneId.of("Asia/Seoul")),
) : MeetingRepository {

    private fun now() = LocalDateTime.now(clock)
    private var sentAt: LocalDateTime? = null

    override suspend fun getSendLinkPreview(): Result<SendLinkPreview> = runCatching {
        SendLinkPreview(
            childName = "지민",
            messageText = "22:04에 차가 도착해요! ○○ 주변에서 만나",
            delivery = LinkDelivery(sentAt, null, null),
            isMock = true,
        )
    }

    override suspend fun sendLink(): Result<LinkDelivery> = runCatching {
        sentAt = sentAt ?: now()
        LinkDelivery(sentAt, null, null)
    }

    override suspend fun getPickupStop(): Result<PickupStopInfo> = runCatching {
        val n = now()
        val delivery = when (childScenario) {
            "NOT_OPENED" -> LinkDelivery(n.minusMinutes(5), null, null)
            else -> LinkDelivery(n.minusMinutes(3), n.minusMinutes(2), n.minusSeconds(30))
        }
        PickupStopInfo(
            childName = "지민",
            inRestrictedZone = true,
            restrictedMessage = "50m 앞 배정된 픽업 장소로 옮겨 주세요",
            allowedStopSec = 60,
            stopStartedAt = n,
            delivery = delivery,
            isMock = true,
        )
    }

    override suspend fun remindArrived(): Result<Unit> = Result.success(Unit)
}
