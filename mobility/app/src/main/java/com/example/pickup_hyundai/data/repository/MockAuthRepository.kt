package com.example.pickup_hyundai.data.repository

import com.example.pickup_hyundai.domain.model.AcademySchedule
import com.example.pickup_hyundai.domain.model.ChildInfoInput
import com.example.pickup_hyundai.domain.model.HomeAddress
import com.example.pickup_hyundai.domain.model.LoginAccount
import com.example.pickup_hyundai.domain.model.LoginOutcome
import com.example.pickup_hyundai.domain.model.OtpRequest
import com.example.pickup_hyundai.domain.model.OtpVerify
import com.example.pickup_hyundai.domain.model.QrPoll
import com.example.pickup_hyundai.domain.model.QrSession
import com.example.pickup_hyundai.domain.model.SetupSummary
import java.time.DayOfWeek
import java.time.LocalTime

/**
 * 서버 설정(RIDEMATE_API_BASE_URL)이 없을 때 쓰는 Mock(화면에 MOCK 표시).
 * QR: debug 빌드의 DEV 버튼(demoAdvance)으로 PENDING → SCANNED → VERIFIED → 승인 순서로 진행. 인증번호는 아무 6자리 통과.
 */
class MockAuthRepository : AuthRepository {
    override val isMock = true
    private var qrStep = 0

    override suspend fun getAccounts() = Result.success(
        listOf(
            LoginAccount("12", "엄마", recentlyUsed = true, canResume = true),
            LoginAccount("9", "아빠", recentlyUsed = false, canResume = true),
        )
    )
    override suspend fun loginWithAccount(id: String) = Result.success(LoginOutcome(setupRequired = false))
    override suspend fun issueQr(): Result<QrSession> {
        qrStep = 0
        return Result.success(QrSession("https://example.invalid/qr?code=MOCK", "0427", 180, 3))
    }
    override suspend fun pollQr(): Result<QrPoll> = Result.success(
        when (qrStep) {
            0 -> QrPoll.Pending("PENDING")
            1 -> QrPoll.Pending("SCANNED")
            2 -> QrPoll.Pending("VERIFIED")
            else -> QrPoll.Approved(LoginOutcome(setupRequired = false))
        }
    )
    override suspend fun cancelQr(reason: String) = Result.success(Unit)
    override suspend fun requestOtp(phoneDigits: String): Result<OtpRequest> = Result.success(OtpRequest.Sent(180, 60))
    override suspend fun verifyOtp(phoneDigits: String, code: String): Result<OtpVerify> =
        Result.success(OtpVerify.Success(LoginOutcome(setupRequired = false)))
    override suspend fun logoutThisCar() = Result.success(Unit)
    override fun demoAdvance() { qrStep = (qrStep + 1).coerceAtMost(3) }
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
