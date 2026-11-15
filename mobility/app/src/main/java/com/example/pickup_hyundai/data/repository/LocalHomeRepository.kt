package com.example.pickup_hyundai.data.repository

import com.example.pickup_hyundai.data.local.SetupStore
import com.example.pickup_hyundai.domain.model.HomeSummary
import com.example.pickup_hyundai.domain.model.NextPickup
import com.example.pickup_hyundai.domain.model.PickupItem
import com.example.pickup_hyundai.domain.model.WeekDaySchedule
import java.time.Clock
import java.time.DayOfWeek
import java.time.LocalDate
import java.time.LocalDateTime
import kotlinx.coroutines.withTimeoutOrNull
import java.time.ZoneId
import java.time.temporal.TemporalAdjusters

/**
 * 홈(V02) 주간·일간 일정: 초기 설정에서 저장한 시간표(픽업 켠 학원)로 만든다.
 * 시간표가 아직 없으면 [fallback](Mock, MOCK 표시)을 쓴다. 정본에는 일정 조회 API가 없다.
 */
class LocalHomeRepository(
    private val store: SetupStore,
    /** 예시 일정으로 대체할 때만 지정. null이면 시간표가 없을 때 "일정 없음"으로 표시 */
    private val fallback: HomeRepository? = null,
    /** 오늘 일정이 있을 때 추천 출발 시각 조회(픽업 저장소). 3초 안에 못 받으면 마지막으로 받은 값 사용 */
    private val departure: (suspend () -> LocalDateTime?)? = null,
    private val clock: Clock = Clock.system(ZoneId.of("Asia/Seoul")),
) : HomeRepository {

    override suspend fun getHomeSummary(): Result<HomeSummary> {
        val schedules = store.schedules ?: fallback?.let { return it.getHomeSummary() } ?: emptyList()
        return runCatching {
            val child = store.child?.name ?: "아이"
            val enabled = schedules.filter { it.pickupEnabled }
            fun itemsOn(date: LocalDate) = enabled.filter { it.day == date.dayOfWeek }.sortedBy { it.endTime }
                .map { PickupItem(child, it.academyName, it.endTime, null) }

            val today = LocalDate.now(clock)
            val monday = today.with(TemporalAdjusters.previousOrSame(DayOfWeek.MONDAY))
            val todayItems = itemsOn(today)
            val week = (0L..6L).map { off ->
                val d = monday.plusDays(off)
                val items = itemsOn(d)
                WeekDaySchedule(d, items.firstOrNull()?.endTime, (items.size - 1).coerceAtLeast(0))
            }
            val next = if (todayItems.isEmpty()) (1L..7L).asSequence().map { today.plusDays(it) }
                .firstNotNullOfOrNull { d -> itemsOn(d).firstOrNull()?.let { NextPickup(it, d) } } else null
            val dep = if (todayItems.isEmpty()) null else
                (departure?.let { d -> withTimeoutOrNull(3_000) { runCatching { d() }.getOrNull() } }?.also { store.cacheDeparture(it) }
                    ?: store.cachedDepartureFor(today))
            HomeSummary(
                nextDepartureDt = dep,
                firstEndTime = todayItems.firstOrNull()?.endTime,
                todayItems = todayItems,
                week = week,
                nextPickup = next,
                // 시간표 원본이 Mock 시드라면 여전히 예시 데이터 → 설정을 마치기 전까지 MOCK 표시
                isMock = false,
            )
        }
    }
}
