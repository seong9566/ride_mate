package com.example.pickup_hyundai.domain.pickup

import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import com.example.pickup_hyundai.domain.model.AssignedPlan
import com.example.pickup_hyundai.domain.model.DeparturePlan
import com.example.pickup_hyundai.domain.model.PickupSpotCandidate
import com.example.pickup_hyundai.domain.model.WaitingPlace
import com.example.pickup_hyundai.domain.route.GeoPoint
import java.time.LocalDateTime
import java.time.LocalTime

/**
 * 오늘 픽업 한 번의 진행 상태(화면 간 공유, 메모리 전용).
 * 순서: V04 장소 선택 → (V06 대기 장소 → V07 이동 → V07A 대기) → V08 픽업 장소 안내 → V09 만남 연결 → 정차 → V10 집 → V11 기록
 * 좌표·시각은 화면 표시와 내비 요청에만 쓰고, 기록에는 좌표를 남기지 않는다(LocalRecordStore).
 */
class PickupSession {
    var academyName by mutableStateOf<String?>(null)
    var endTime by mutableStateOf<LocalTime?>(null)
    var departureDt by mutableStateOf<LocalDateTime?>(null)
    var pickupName by mutableStateOf<String?>(null)
    var pickupPoint by mutableStateOf<GeoPoint?>(null)
    var walkMin by mutableStateOf<Int?>(null)
    /** 정본 enforcement.status == RESTRICTED(학원 앞 단속 구역) */
    var restricted by mutableStateOf<Boolean?>(null)
    var plan by mutableStateOf<AssignedPlan?>(null)
    /** V04에서 부모가 고른 후보(AddPickupPlan 좌표) */
    var chosenSpot by mutableStateOf<PickupSpotCandidate?>(null)
    var waitingPlace by mutableStateOf<WaitingPlace?>(null)
    /** 학원 근처 → 집 차량 소요(분). 정본 없음(Mock 값만 있음) */
    var toHomeMin by mutableStateOf<Int?>(null)

    var departedAt by mutableStateOf<LocalDateTime?>(null)
    var waitingArrivedAt by mutableStateOf<LocalDateTime?>(null)
    var linkSentAt by mutableStateOf<LocalDateTime?>(null)
    /** "나중에"를 누르면 같은 구간에서 다시 묻지 않는다 */
    var sendPromptDismissed by mutableStateOf(false)
    var arrivedAtPickupAt by mutableStateOf<LocalDateTime?>(null)
    var boardedAt by mutableStateOf<LocalDateTime?>(null)
    var homeArrivedAt by mutableStateOf<LocalDateTime?>(null)

    /** 배정 도착 시각(계획) → 없으면 하원 시각 */
    val targetArrival: LocalDateTime?
        get() = plan?.slotDt ?: endTime?.let { e -> (departureDt ?: LocalDateTime.now()).toLocalDate().atTime(e) }

    fun applyDeparture(p: DeparturePlan) {
        academyName = p.academyName
        endTime = p.endTime
        departureDt = p.departureDt
        if (plan?.pickupName == null && chosenSpot == null) {
            pickupName = p.pickupName
            pickupPoint = GeoPoint(p.pickupLatitude, p.pickupLongitude)
            walkMin = p.walkMin
        }
        p.enforcementRestricted?.let { restricted = it }
    }

    fun applyPlan(p: AssignedPlan) {
        plan = p
        p.pickupName?.let { pickupName = it }
        if (p.latitude != null && p.longitude != null) pickupPoint = GeoPoint(p.latitude, p.longitude)
        p.walkMin?.let { walkMin = it }
    }

    fun reset() {
        academyName = null; endTime = null; departureDt = null; pickupName = null; pickupPoint = null
        walkMin = null; restricted = null; plan = null; chosenSpot = null; waitingPlace = null; toHomeMin = null
        departedAt = null; waitingArrivedAt = null; linkSentAt = null; sendPromptDismissed = false
        arrivedAtPickupAt = null; boardedAt = null; homeArrivedAt = null
    }
}
