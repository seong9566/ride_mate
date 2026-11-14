package com.example.pickup_hyundai.core.time

import java.time.DayOfWeek
import java.time.Duration
import java.time.LocalDate
import java.time.LocalTime
import java.time.ZoneId
import java.time.format.DateTimeFormatter

object KstFormat {
    val ZONE: ZoneId = ZoneId.of("Asia/Seoul")
    private val HM: DateTimeFormatter = DateTimeFormatter.ofPattern("HH:mm")

    fun hm(time: LocalTime): String = time.format(HM)

    /** "6시간 10분" / "25분" / "1분 미만". 음수면 null(지난 시각) */
    fun remaining(duration: Duration): String? {
        if (duration.isNegative) return null
        val totalMin = duration.toMinutes()
        val h = totalMin / 60
        val m = totalMin % 60
        return when {
            h > 0 && m > 0 -> "${h}시간 ${m}분"
            h > 0 -> "${h}시간"
            m > 0 -> "${m}분"
            else -> "1분 미만"
        }
    }

    fun dayShort(day: DayOfWeek): String = when (day) {
        DayOfWeek.MONDAY -> "월"
        DayOfWeek.TUESDAY -> "화"
        DayOfWeek.WEDNESDAY -> "수"
        DayOfWeek.THURSDAY -> "목"
        DayOfWeek.FRIDAY -> "금"
        DayOfWeek.SATURDAY -> "토"
        DayOfWeek.SUNDAY -> "일"
    }

    /** 다음 픽업 날짜 표기: "내일 · 화요일" / "11월 20일 · 금요일" */
    fun relativeDay(date: LocalDate, today: LocalDate): String {
        val dayName = dayShort(date.dayOfWeek) + "요일"
        return when (date) {
            today -> "오늘 · $dayName"
            today.plusDays(1) -> "내일 · $dayName"
            else -> "${date.monthValue}월 ${date.dayOfMonth}일 · $dayName"
        }
    }
}
