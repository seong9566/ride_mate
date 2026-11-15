package com.example.pickup_hyundai.data.repository

import com.example.pickup_hyundai.domain.model.DriveProgress
import com.example.pickup_hyundai.domain.model.HomeDrive
import com.example.pickup_hyundai.domain.model.PickupRecord
import com.example.pickup_hyundai.domain.model.WaitingDrive
import com.example.pickup_hyundai.domain.model.WaitingPlace
import com.example.pickup_hyundai.domain.pickup.PickupSession
import java.time.Clock
import java.time.Duration
import java.time.LocalDate
import java.time.ZoneId

/**
 * 주행·기록 데이터: 진행 중인 픽업([session])에 기록된 실제 시각(출발·탑승·집 도착)을 쓴다.
 * 남은 시간·거리는 화면에서 NaviHelper/FusedLocation 값으로 바꿔 끼운다(withLiveProgress). 여기서는 비워 둔다.
 */
class MockDriveRepository(
    private val session: PickupSession? = null,
    private val childName: () -> String? = { null },
    private val clock: Clock = Clock.system(ZoneId.of("Asia/Seoul")),
) : DriveRepository {

    private val empty = DriveProgress(remainMin = null, distanceKm = null, arrivalTime = null, guidanceActive = false)

    override suspend fun getWaitingDrive(place: WaitingPlace): Result<WaitingDrive> = runCatching {
        WaitingDrive(place = place, progress = empty.copy(remainMin = place.driveMin), isMock = false)
    }

    override suspend fun getHomeDrive(): Result<HomeDrive> = runCatching {
        HomeDrive(
            homeName = "우리 집",
            childName = childName()?.takeIf { it.isNotBlank() } ?: "아이",
            boardedAt = session?.boardedAt,
            boardedPlaceName = session?.pickupName,
            progress = empty,
            isMock = false,
        )
    }

    override suspend fun getTodayRecord(): Result<PickupRecord> = runCatching {
        val s = session
        val departed = s?.departedAt
        val boarded = s?.boardedAt
        val home = s?.homeArrivedAt
        val waitFrom = s?.waitingArrivedAt ?: s?.arrivedAtPickupAt
        PickupRecord(
            date = LocalDate.now(clock),
            departedAt = departed?.toLocalTime(),
            departurePlaceName = null,
            boardedAt = boarded?.toLocalTime(),
            boardedPlaceName = s?.pickupName,
            homeArrivedAt = home?.toLocalTime(),
            totalMin = if (departed != null && home != null) Duration.between(departed, home).toMinutes().toInt() else null,
            waitMin = if (waitFrom != null && boarded != null) Duration.between(waitFrom, boarded).toMinutes().toInt().coerceAtLeast(0) else null,
            loopCount = 0,
        )
    }
}
