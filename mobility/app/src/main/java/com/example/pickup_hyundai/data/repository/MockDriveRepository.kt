package com.example.pickup_hyundai.data.repository

import com.example.pickup_hyundai.domain.model.DriveProgress
import com.example.pickup_hyundai.domain.model.HomeDrive
import com.example.pickup_hyundai.domain.model.PickupRecord
import com.example.pickup_hyundai.domain.model.WaitingDrive
import com.example.pickup_hyundai.domain.model.WaitingPlace
import java.time.Clock
import java.time.LocalDate
import java.time.LocalTime
import java.time.ZoneId

/** 디자인 예시 값 Mock(isMock = true) */
class MockDriveRepository(
    private val clock: Clock = Clock.system(ZoneId.of("Asia/Seoul")),
) : DriveRepository {

    override suspend fun getWaitingDrive(place: WaitingPlace): Result<WaitingDrive> = runCatching {
        WaitingDrive(
            place = place,
            progress = DriveProgress(remainMin = place.driveMin ?: 3, distanceKm = 0.9, arrivalTime = LocalTime.of(21, 22), guidanceActive = true),
            isMock = true,
        )
    }

    override suspend fun getHomeDrive(): Result<HomeDrive> = runCatching {
        val today = LocalDate.now(clock)
        HomeDrive(
            homeName = "○○아파트",
            childName = "지민",
            boardedAt = today.atTime(22, 8),
            boardedPlaceName = "○○ 앞",
            progress = DriveProgress(remainMin = 18, distanceKm = 6.4, arrivalTime = LocalTime.of(22, 28), guidanceActive = true),
            isMock = true,
        )
    }

    // 디자인 이미지의 "집 도착 22:04"는 탑승(22:08)보다 빨라 오기로 보고, 출발 21:50 + 총 38분 = 22:28로 맞췄다.
    override suspend fun getTodayRecord(): Result<PickupRecord> = runCatching {
        PickupRecord(
            date = LocalDate.now(clock),
            departedAt = LocalTime.of(21, 50),
            departurePlaceName = "도곡초 공영주차장",
            boardedAt = LocalTime.of(22, 8),
            boardedPlaceName = "○○ 앞",
            homeArrivedAt = LocalTime.of(22, 28),
            totalMin = 38,
            waitMin = 17,
            loopCount = 1,
        )
    }
}
