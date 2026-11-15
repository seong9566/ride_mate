package com.example.pickup_hyundai.data.repository

import com.example.pickup_hyundai.domain.model.LinkDelivery
import com.example.pickup_hyundai.domain.model.PickupStopInfo
import com.example.pickup_hyundai.domain.model.SendLinkPreview

/**
 * V09 만남 연결·픽업 정차 데이터 소스.
 * 정본(RideMate_Integrated_API_and_Flow.md)에는 차량 앱의 문자 발송·전달 상태 조회·다시 알리기 API가 없다
 * (08_USER_FLOW §5 P0). 모두 Mock이며 실제 API 확정 후 교체한다.
 */
interface MeetingRepository {
    suspend fun getSendLinkPreview(): Result<SendLinkPreview>
    /** 성공 응답을 받은 뒤에만 "보냄"으로 표시한다(§4-3) */
    suspend fun sendLink(): Result<LinkDelivery>
    suspend fun getPickupStop(): Result<PickupStopInfo>
    suspend fun remindArrived(): Result<Unit>
}
