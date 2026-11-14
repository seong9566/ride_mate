package com.example.pickup_hyundai.data.repository

import com.example.pickup_hyundai.domain.model.AcademySchedule
import com.example.pickup_hyundai.domain.model.ChildInfoInput
import com.example.pickup_hyundai.domain.model.HomeAddress
import com.example.pickup_hyundai.domain.model.LoginConfirm
import com.example.pickup_hyundai.domain.model.LoginAccount
import com.example.pickup_hyundai.domain.model.SetupSummary

/**
 * A00 로그인 / A01 초기 설정 데이터 소스.
 * 차량 앱의 QR 발급, 토큰, 계정 목록, 아이/시간표/집 등록 API (미정) -> Mock.
 */
interface AuthRepository {
    suspend fun getAccounts(): Result<List<LoginAccount>>
    suspend fun loginWithAccount(id: String): Result<Unit>
    /** 휴대폰 확인 요청. accountId = 저장된 계정 선택, null = QR 스캔으로 시작 */
    suspend fun requestLoginConfirm(accountId: String?): Result<LoginConfirm>
    /** 휴대폰에서 '로그인 허용'됨 → true면 초기 설정 필요(신규) */
    suspend fun completeLoginConfirm(): Result<Boolean>
    suspend fun requestOtp(phoneDigits: String): Result<Int>
    suspend fun verifyOtp(phoneDigits: String, code: String): Result<Boolean>
    suspend fun logoutThisCar(): Result<Unit>
}

interface SetupRepository {
    suspend fun saveChild(child: ChildInfoInput): Result<Unit>
    suspend fun getSchedules(): Result<List<AcademySchedule>>
    suspend fun setPickupEnabled(id: String, enabled: Boolean): Result<Unit>
    suspend fun getNaviHome(): Result<HomeAddress?>
    suspend fun saveHome(home: HomeAddress): Result<Unit>
    suspend fun getSummary(): Result<SetupSummary>
}
