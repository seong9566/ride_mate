package com.example.pickup_hyundai.data.repository

import com.example.pickup_hyundai.domain.model.HomeDrive
import com.example.pickup_hyundai.domain.model.PickupRecord
import com.example.pickup_hyundai.domain.model.WaitingDrive
import com.example.pickup_hyundai.domain.model.WaitingPlace

/**
 * V07·V10·V11 주행/완료 데이터 소스.
 * 남은 시간·거리는 NaviHelper 이벤트(5단계), 탑승·완료 시각은 픽업 상태 API(정본 없음)에서 와야 한다 → 현재 Mock.
 */
interface DriveRepository {
    suspend fun getWaitingDrive(place: WaitingPlace): Result<WaitingDrive>
    suspend fun getHomeDrive(): Result<HomeDrive>
    suspend fun getTodayRecord(): Result<PickupRecord>
}
