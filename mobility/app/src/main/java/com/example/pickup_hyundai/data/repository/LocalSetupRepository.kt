package com.example.pickup_hyundai.data.repository

import com.example.pickup_hyundai.core.time.KstFormat
import com.example.pickup_hyundai.data.local.SetupStore
import com.example.pickup_hyundai.domain.model.AcademySchedule
import com.example.pickup_hyundai.domain.model.ChildInfoInput
import com.example.pickup_hyundai.domain.model.HomeAddress
import com.example.pickup_hyundai.domain.model.SetupSummary
import com.example.pickup_hyundai.platform.navi.NaviPlace

/**
 * 초기 설정 저장소: 로컬 저장(SetupStore) + 내비 '집' 즐겨찾기(NaviHelper getBookmarkInfo).
 * - 시간표 원본(학원·요일·하원 시각)을 받는 API가 정본에 없어, 처음에는 [seed](디자인 예시 Mock)로 채운다.
 *   사용자가 픽업 켜기/끄기를 바꾸면 로컬에 저장되고 홈 일정에 반영된다.
 */
class LocalSetupRepository(
    private val store: SetupStore,
    private val seed: MockSetupRepository,
    /** NaviController.state.homeBookmark */
    private val naviHome: () -> NaviPlace?,
) : SetupRepository {

    override suspend fun saveChild(child: ChildInfoInput): Result<Unit> = runCatching { store.child = child }

    override suspend fun getSchedules(): Result<List<AcademySchedule>> = runCatching {
        store.schedules ?: seed.getSchedules().getOrThrow().also { store.schedules = it }
    }

    override suspend fun setPickupEnabled(id: String, enabled: Boolean): Result<Unit> = runCatching {
        val list = getSchedules().getOrThrow()
        store.schedules = list.map { if (it.id == id) it.copy(pickupEnabled = enabled) else it }
    }

    /** 내비 '집' 즐겨찾기 우선 → 저장된 집 → 없음(null: 주소 검색 필요) */
    override suspend fun getNaviHome(): Result<HomeAddress?> = runCatching {
        naviHome()?.let { HomeAddress(it.address.ifBlank { it.name }, it.latitude, it.longitude, fromNaviBookmark = true) }
            ?: store.home
    }

    override suspend fun saveHome(home: HomeAddress): Result<Unit> = runCatching { store.home = home }

    override suspend fun getSummary(): Result<SetupSummary> = runCatching {
        val child = store.child?.name ?: "아이"
        val rows = getSchedules().getOrThrow().filter { it.pickupEnabled }
            .groupBy { it.academyName to it.endTime }
            .map { (k, list) ->
                "$child · ${k.first}" to "${list.sortedBy { it.day }.joinToString(" · ") { KstFormat.dayShort(it.day) }} ${KstFormat.hm(k.second)}"
            }
        // 학원별 픽업 장소는 정본 DetailDepartureGuide가 당일에만 알려 준다 → 요약에는 표시하지 않음
        SetupSummary(weeklyRows = rows, pickupPlaceRows = emptyList())
    }
}
