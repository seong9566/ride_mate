package com.example.pickup_hyundai.core.time

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test
import java.time.Duration
import java.time.LocalDate

class KstFormatTest {
    @Test fun remaining_hoursAndMinutes() = assertEquals("6시간 10분", KstFormat.remaining(Duration.ofMinutes(370)))
    @Test fun remaining_hoursOnly() = assertEquals("2시간", KstFormat.remaining(Duration.ofMinutes(120)))
    @Test fun remaining_minutesOnly() = assertEquals("25분", KstFormat.remaining(Duration.ofMinutes(25)))
    @Test fun remaining_underMinute() = assertEquals("1분 미만", KstFormat.remaining(Duration.ofSeconds(30)))
    @Test fun remaining_past() = assertNull(KstFormat.remaining(Duration.ofMinutes(-1)))

    @Test fun relativeDay_tomorrow() {
        val today = LocalDate.of(2026, 11, 16) // 월요일
        assertEquals("내일 · 화요일", KstFormat.relativeDay(today.plusDays(1), today))
    }
}
