package com.example.pickup_hyundai.data.repository

import com.example.pickup_hyundai.domain.model.AcademySchedule
import com.example.pickup_hyundai.domain.model.ChildInfoInput
import com.example.pickup_hyundai.domain.model.HomeAddress
import com.example.pickup_hyundai.domain.model.LoginConfirm
import com.example.pickup_hyundai.domain.model.LoginAccount
import com.example.pickup_hyundai.domain.model.SetupSummary
import java.time.DayOfWeek
import java.time.LocalTime

/** 디자인 예시 값 Mock. 인증번호는 Mock에서 어떤 6자리든 통과한다. */
class MockAuthRepository : AuthRepository {
    override suspend fun getAccounts() = Result.success(
        listOf(
            LoginAccount("mom", "엄마", "김*주", recentlyUsed = true),
            LoginAccount("dad", "아빠", "이*은", recentlyUsed = false),
        )
    )
    override suspend fun loginWithAccount(id: String) = Result.success(Unit)
    override suspend fun requestLoginConfirm(accountId: String?) = Result.success(LoginConfirm("4821", 180))
    override suspend fun completeLoginConfirm() = Result.success(false)
    override suspend fun requestOtp(phoneDigits: String) = Result.success(180)
    override suspend fun verifyOtp(phoneDigits: String, code: String) = Result.success(true)
    override suspend fun logoutThisCar() = Result.success(Unit)
}

class MockSetupRepository : SetupRepository {
    private val schedules = mutableListOf(
        AcademySchedule("m-mon", "수학학원", "대치동 ○○빌딩 4층", DayOfWeek.MONDAY, LocalTime.of(22, 0), true),
        AcademySchedule("e-tue", "영어학원", "대치동 △△타워 2층", DayOfWeek.TUESDAY, LocalTime.of(21, 30), true),
        AcademySchedule("m-wed", "수학학원", "대치동 ○○빌딩 4층", DayOfWeek.WEDNESDAY, LocalTime.of(22, 0), true),
        AcademySchedule("e-wed", "영어학원", "대치동 △△타워 2층", DayOfWeek.WEDNESDAY, LocalTime.of(19, 30), false),
        AcademySchedule("m-fri", "수학학원", "대치동 ○○빌딩 4층", DayOfWeek.FRIDAY, LocalTime.of(21, 30), true),
    )

    override suspend fun saveChild(child: ChildInfoInput) = Result.success(Unit)
    override suspend fun getSchedules() = Result.success(schedules.toList())
    override suspend fun setPickupEnabled(id: String, enabled: Boolean): Result<Unit> = runCatching {
        val i = schedules.indexOfFirst { it.id == id }
        if (i >= 0) schedules[i] = schedules[i].copy(pickupEnabled = enabled)
    }
    override suspend fun getNaviHome() = Result.success(HomeAddress("서울 강남구 도곡로 ○○, 101동", 37.4920, 127.0460, fromNaviBookmark = true))
    override suspend fun saveHome(home: HomeAddress) = Result.success(Unit)
    override suspend fun getSummary() = Result.success(
        SetupSummary(
            weeklyRows = listOf("지민 · 수학학원" to "월 · 수 · 금 22:00", "지민 · 영어학원" to "화 · 목 21:30"),
            pickupPlaceRows = listOf("수학학원" to "○○ 앞 · 걸어서 3분", "영어학원" to "△△빌딩 앞 · 걸어서 2분"),
        )
    )
}
