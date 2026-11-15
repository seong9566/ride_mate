package com.example.pickup_hyundai.data.repository

import com.example.pickup_hyundai.core.net.ApiException
import com.example.pickup_hyundai.core.time.KstFormat
import com.example.pickup_hyundai.data.remote.ChildApi
import com.example.pickup_hyundai.data.remote.ScheduleApi
import com.example.pickup_hyundai.data.local.SetupStore
import com.example.pickup_hyundai.domain.model.AcademyInput
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
    /** 예시 시간표로 시작할 때만 지정. null이면 빈 시간표에서 시작(더미 데이터 없음) */
    private val seed: MockSetupRepository? = null,
    /** NaviController.state.homeBookmark */
    private val naviHome: () -> NaviPlace?,
    /** 서버 연동 시 시간표 저장 API. null이면 차량 내 저장만 */
    private val scheduleApi: ScheduleApi? = null,
    /** 서버 연동 시 아이 정보 저장 API */
    private val childApi: ChildApi? = null,
) : SetupRepository {

    /**
     * 아이 정보 저장. 서버 연동 중이면 AddChild(POST /api/v1/ChildInfo/M/sign/AddChild)로 저장한 뒤 차량에도 보관한다.
     * 409(이미 아이가 있음)는 "이미 저장됨"으로 보고 다음 단계로, 102·103은 서버 message를 그대로 보여 준다.
     */
    override suspend fun saveChild(child: ChildInfoInput): Result<Unit> = try {
        val api = childApi
        if (api == null) {
            store.child = child
        } else {
            val r = api.addChild(child)
            when (r.code) {
                200 -> store.child = child.copy(name = r.childName ?: child.name.trim())
                // 먼저 저장된 아이가 기준(번호는 가려져 와서 입력값을 보관)
                409 -> store.child = child.copy(name = r.childName ?: child.name.trim())
                else -> throw IllegalStateException(r.message ?: "아이 정보를 저장하지 못했어요")
            }
        }
        Result.success(Unit)
    } catch (e: ApiException) {
        Result.failure(IllegalStateException(if (e.kind == com.example.pickup_hyundai.core.net.ApiErrorKind.BAD_REQUEST) "입력한 내용을 확인해 주세요" else e.userMessage, e))
    } catch (e: Exception) {
        Result.failure(e)
    }

    override suspend fun getSchedules(): Result<List<AcademySchedule>> = runCatching {
        store.schedules ?: (seed?.getSchedules()?.getOrThrow() ?: emptyList()).also { store.schedules = it }
    }

    override suspend fun setPickupEnabled(id: String, enabled: Boolean): Result<Unit> = runCatching {
        val list = getSchedules().getOrThrow()
        store.schedules = list.map { if (it.id == id) it.copy(pickupEnabled = enabled) else it }
    }

    /**
     * 학원 추가. 서버 연동 중이면 AddSchedule(POST /api/v1/Schedule/M/sign/AddSchedule)로 저장하고 응답을 차량에도 반영한다.
     * code 102·103·104는 서버 message를 그대로 보여 주고 저장하지 않는다.
     * code 101(서버에 아이 정보 없음)은 아이 등록 API가 아직 연결되지 않아 생기는 경우라, 이 차에만 저장하고 그 사실을 알린다.
     */
    override suspend fun addAcademy(input: AcademyInput): Result<String?> = try {
        val list = getSchedules().getOrThrow()
        val api = scheduleApi
        var notice: String? = null
        val added: List<AcademySchedule> = if (api == null) local(list, input) else {
            val r = api.addSchedule(input)
            when (r.code) {
                200 -> {
                    val sv = r.schedule ?: throw IllegalStateException(r.message ?: "시간표를 저장하지 못했어요")
                    val place = listOf(sv.academyName, sv.address).filter { it.isNotBlank() }.distinct().joinToString(" · ")
                    sv.days.map { d -> AcademySchedule("srv-${d.scheduleDaySeq}", sv.displayName, place, d.day, sv.endTime, d.pickupYn) }
                }
                101 -> {
                    notice = "서버에 아이 정보가 없어 이 차에만 저장했어요. 아이 정보를 다시 저장해 주세요"
                    local(list, input)
                }
                else -> throw IllegalStateException(r.message ?: "시간표를 저장하지 못했어요")
            }
        }
        store.schedules = list + added
        Result.success(notice)
    } catch (e: ApiException) {
        Result.failure(IllegalStateException(e.userMessage, e))
    } catch (e: Exception) {
        Result.failure(e)
    }

    private fun local(list: List<AcademySchedule>, input: AcademyInput): List<AcademySchedule> {
        // 서버 code 103에 해당하는 확인: 같은 요일에 하원 시각이 40분 이내인 시간표가 있으면 막는다
        val clash = list.firstOrNull { it.day in input.days && kotlin.math.abs(java.time.Duration.between(it.endTime, input.endTime).toMinutes()) <= 40 }
        if (clash != null) throw IllegalStateException("${KstFormat.dayShort(clash.day)}요일에 시간이 가까운 시간표가 있어요(${clash.academyName} ${KstFormat.hm(clash.endTime)})")
        val stamp = System.currentTimeMillis()
        val place = listOf(input.placeName, input.address).filter { it.isNotBlank() }.distinct().joinToString(" · ")
        return input.days.sorted().map { d ->
            AcademySchedule("local-$stamp-${d.value}", input.displayName.trim(), place, d, input.endTime, pickupEnabled = true)
        }
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
