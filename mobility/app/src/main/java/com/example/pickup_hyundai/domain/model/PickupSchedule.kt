package com.example.pickup_hyundai.domain.model

import java.time.DayOfWeek
import java.time.LocalDate
import java.time.LocalDateTime
import java.time.LocalTime

/** 하원 픽업 1건 */
data class PickupItem(
    val childName: String,
    val academyName: String,
    val endTime: LocalTime,
    val pickupPlaceName: String?,
)

/** 주간 일정 칸 하나(월~일). 일정이 없으면 firstEndTime = null */
data class WeekDaySchedule(
    val date: LocalDate,
    val firstEndTime: LocalTime?,
    /** 같은 날 추가 일정 수(디자인의 "+1") */
    val extraCount: Int,
) {
    val dayOfWeek: DayOfWeek get() = date.dayOfWeek
}

data class NextPickup(
    val item: PickupItem,
    val date: LocalDate,
)

/** 홈 화면(V02) 데이터 */
data class HomeSummary(
    /** 오늘 첫 픽업의 추천 출발 시각. 정본 API: DetailDepartureGuide.departureDt */
    val nextDepartureDt: LocalDateTime?,
    val firstEndTime: LocalTime?,
    val todayItems: List<PickupItem>,
    val week: List<WeekDaySchedule>,
    /** 오늘 일정이 없을 때 보여줄 다음 픽업 */
    val nextPickup: NextPickup?,
    /** 실제 API가 아닌 Mock 데이터 여부(화면에 MOCK 라벨 표시) */
    val isMock: Boolean,
)
