package com.example.pickup_hyundai.data.repository

import com.example.pickup_hyundai.core.time.KstFormat
import com.example.pickup_hyundai.domain.model.LinkDelivery
import com.example.pickup_hyundai.domain.model.PickupStopInfo
import com.example.pickup_hyundai.domain.model.SendLinkPreview
import com.example.pickup_hyundai.domain.pickup.PickupSession
import java.time.Clock
import java.time.LocalDateTime
import java.time.ZoneId

/**
 * 서버 없이 쓰는 만남 연결·정차 데이터. 문자 발송·전달 상태 API가 없어 실제 문자는 나가지 않는다.
 * 이름·시각·장소는 진행 중인 픽업([session])과 초기 설정의 아이 이름([childName])을 쓴다.
 * 아이 반응(열람 → 지금 나가요)은 보낸 시각 기준으로 시간이 지나며 진행되는 것으로 가정한다.
 * @param childScenario LEAVING: 열람 후 나오는 중 / NOT_OPENED: 아이 미확인
 */
class MockMeetingRepository(
    private val childScenario: String = "LEAVING",
    private val session: PickupSession? = null,
    private val childName: () -> String? = { null },
    private val clock: Clock = Clock.system(ZoneId.of("Asia/Seoul")),
) : MeetingRepository {

    private fun now() = LocalDateTime.now(clock)
    private var sentAt: LocalDateTime? = null
    private var stopStartedAt: LocalDateTime? = null
    private fun name() = childName()?.takeIf { it.isNotBlank() } ?: "아이"

    override suspend fun getSendLinkPreview(): Result<SendLinkPreview> = runCatching {
        val arrive = session?.targetArrival?.toLocalTime()?.let(KstFormat::hm)
        val place = session?.pickupName ?: "픽업 장소"
        SendLinkPreview(
            childName = name(),
            messageText = (arrive?.let { "${it}에 차가 도착해요! " } ?: "곧 차가 도착해요! ") + "$place 주변에서 만나",
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
        val started = stopStartedAt ?: n.also { stopStartedAt = it }
        val sent = sentAt ?: session?.linkSentAt
        val delivery = when {
            sent == null -> LinkDelivery(null, null, null)
            childScenario == "NOT_OPENED" -> LinkDelivery(sent, null, null)
            // 보낸 뒤 1분쯤 열람, 정차 직전에 "지금 나가요"
            else -> {
                val opened = sent.plusMinutes(1).let { if (it.isAfter(n)) n else it }
                val leaving = n.minusSeconds(20).let { if (it.isBefore(opened)) opened else it }
                LinkDelivery(sent, opened, leaving)
            }
        }
        PickupStopInfo(
            childName = name(),
            inRestrictedZone = session?.restricted == true,
            restrictedMessage = "배정된 픽업 장소에서 잠시만 정차해 주세요",
            allowedStopSec = 60,
            stopStartedAt = started,
            delivery = delivery,
            isMock = true,
        )
    }

    override suspend fun remindArrived(): Result<Unit> = Result.success(Unit)

    /** 새 픽업을 시작할 때 초기화 */
    fun reset() { sentAt = null; stopStartedAt = null }
}
