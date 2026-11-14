package com.example.pickup_hyundai.data.repository

import com.example.pickup_hyundai.domain.model.HomeSummary
import com.example.pickup_hyundai.domain.model.NextPickup
import com.example.pickup_hyundai.domain.model.PickupItem
import com.example.pickup_hyundai.domain.model.WeekDaySchedule
import java.time.Clock
import java.time.DayOfWeek
import java.time.LocalDate
import java.time.LocalTime
import java.time.ZoneId
import java.time.temporal.TemporalAdjusters

/**
 * 디자인 이미지의 예시 값으로 만든 Mock. 실제 서비스 데이터가 아니다(isMock = true).
 * @param hasTodaySchedule false면 "오늘은 픽업 일정이 없어요" 상태를 만든다.
 */
class MockHomeRepository(
    private val hasTodaySchedule: Boolean = true,
    private val clock: Clock = Clock.system(ZoneId.of("Asia/Seoul")),
) : HomeRepository {

    override suspend fun getHomeSummary(): Result<HomeSummary> = runCatching {
        val today = LocalDate.now(clock)
        val monday = today.with(TemporalAdjusters.previousOrSame(DayOfWeek.MONDAY))

        // 요일별 하원 시각(디자인 예시). 오늘 칸은 hasTodaySchedule로 결정
        val sample = mapOf(
            DayOfWeek.MONDAY to LocalTime.of(22, 0),
            DayOfWeek.TUESDAY to LocalTime.of(21, 30),
            DayOfWeek.WEDNESDAY to LocalTime.of(22, 0),
            DayOfWeek.THURSDAY to LocalTime.of(22, 0),
            DayOfWeek.FRIDAY to LocalTime.of(21, 30),
        )
        val todayItems = if (hasTodaySchedule) listOf(
            PickupItem("지민", "수학학원", LocalTime.of(22, 0), "OO 앞 우리 픽업 장소"),
            PickupItem("지민", "영어학원", LocalTime.of(22, 40), "△△빌딩 앞"),
        ) else emptyList()

        val week = (0L..6L).map { offset ->
            val date = monday.plusDays(offset)
            if (date == today) {
                WeekDaySchedule(date, todayItems.firstOrNull()?.endTime, (todayItems.size - 1).coerceAtLeast(0))
            } else {
                WeekDaySchedule(date, sample[date.dayOfWeek], 0)
            }
        }

        val first = todayItems.firstOrNull()
        HomeSummary(
            nextDepartureDt = first?.let { today.atTime(21, 29) },
            firstEndTime = first?.endTime,
            todayItems = todayItems,
            week = week,
            nextPickup = if (first == null) {
                val tomorrow = today.plusDays(1)
                NextPickup(PickupItem("지민", "영어학원", LocalTime.of(21, 30), "△△빌딩 앞"), tomorrow)
            } else null,
            isMock = true,
        )
    }
}
