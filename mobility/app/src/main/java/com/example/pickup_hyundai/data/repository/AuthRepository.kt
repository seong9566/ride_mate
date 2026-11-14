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

/**
 * A00 로그인 데이터 소스. 실제: RemoteAuthRepository(WAY:ME 인증 API M-01~M-08), 서버 설정이 없으면 MockAuthRepository.
 */
interface AuthRepository {
    val isMock: Boolean
    /** M-06 이 차에 로그인한 계정 목록 */
    suspend fun getAccounts(): Result<List<LoginAccount>>
    /** M-04 계정 선택(저장된 refresh token으로 로그인) */
    suspend fun loginWithAccount(id: String): Result<LoginOutcome>
    /** M-01 QR 발급(이전 QR이 있으면 먼저 취소) */
    suspend fun issueQr(): Result<QrSession>
    /** M-02 승인 확인 */
    suspend fun pollQr(): Result<QrPoll>
    /** M-03 QR 취소. reason: USER / DRIVING */
    suspend fun cancelQr(reason: String): Result<Unit>
    /** M-07 차량 OTP 발송 */
    suspend fun requestOtp(phoneDigits: String): Result<OtpRequest>
    /** M-08 차량 OTP 확인 */
    suspend fun verifyOtp(phoneDigits: String, code: String): Result<OtpVerify>
    /** M-05 현재 계정 로그아웃 + 이 차의 토큰 삭제 */
    suspend fun logoutThisCar(): Result<Unit>
    /** 시연(Mock 전용): QR 스캔·휴대폰 허용을 한 단계 진행 */
    fun demoAdvance() {}
}

interface SetupRepository {
    suspend fun saveChild(child: ChildInfoInput): Result<Unit>
    suspend fun getSchedules(): Result<List<AcademySchedule>>
    suspend fun setPickupEnabled(id: String, enabled: Boolean): Result<Unit>
    suspend fun getNaviHome(): Result<HomeAddress?>
    suspend fun saveHome(home: HomeAddress): Result<Unit>
    suspend fun getSummary(): Result<SetupSummary>
}
