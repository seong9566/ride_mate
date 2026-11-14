package com.example.pickup_hyundai.data.local

import android.content.Context
import com.example.pickup_hyundai.domain.model.AcademySchedule
import com.example.pickup_hyundai.domain.model.ChildInfoInput
import com.example.pickup_hyundai.domain.model.HomeAddress
import org.json.JSONArray
import org.json.JSONObject
import java.time.DayOfWeek
import java.time.LocalDate
import java.time.LocalDateTime
import java.time.LocalTime

/**
 * 초기 설정(아이·시간표·집)의 차량 내 로컬 저장소(앱 전용 SharedPreferences, 네트워크 전송 없음).
 * 차량 앱의 아이/시간표/집 등록 API가 정본에 없어 서버 저장은 하지 않는다(API 확정 시 Repository에서 동기화).
 */
class SetupStore(context: Context) {
    private val prefs = context.applicationContext.getSharedPreferences("wayme_setup", Context.MODE_PRIVATE)

    var child: ChildInfoInput?
        get() = prefs.getString(K_CHILD, null)?.let { runCatching { JSONObject(it) }.getOrNull() }?.let {
            ChildInfoInput(it.optString("name"), it.optString("phone"), it.optString("birth"))
        }
        set(v) = prefs.edit().apply {
            if (v == null) remove(K_CHILD)
            else putString(K_CHILD, JSONObject().put("name", v.name).put("phone", v.phoneDigits).put("birth", v.birthDigits).toString())
        }.apply()

    /** null = 아직 시간표를 저장한 적 없음 */
    var schedules: List<AcademySchedule>?
        get() = prefs.getString(K_SCHEDULES, null)?.let { raw ->
            runCatching {
                val arr = JSONArray(raw)
                (0 until arr.length()).map { i ->
                    val o = arr.getJSONObject(i)
                    AcademySchedule(
                        id = o.getString("id"),
                        academyName = o.getString("academy"),
                        address = o.optString("address"),
                        day = DayOfWeek.valueOf(o.getString("day")),
                        endTime = LocalTime.parse(o.getString("end")),
                        pickupEnabled = o.optBoolean("enabled", true),
                    )
                }
            }.getOrNull()
        }
        set(v) = prefs.edit().apply {
            if (v == null) remove(K_SCHEDULES)
            else putString(K_SCHEDULES, JSONArray().apply {
                v.forEach { s ->
                    put(JSONObject().put("id", s.id).put("academy", s.academyName).put("address", s.address)
                        .put("day", s.day.name).put("end", s.endTime.toString()).put("enabled", s.pickupEnabled))
                }
            }.toString())
        }.apply()

    var home: HomeAddress?
        get() = prefs.getString(K_HOME, null)?.let { runCatching { JSONObject(it) }.getOrNull() }?.let {
            HomeAddress(it.optString("address"), it.optDouble("lat"), it.optDouble("lng"), it.optBoolean("navi"))
        }
        set(v) = prefs.edit().apply {
            if (v == null) remove(K_HOME)
            else putString(K_HOME, JSONObject().put("address", v.address).put("lat", v.latitude).put("lng", v.longitude).put("navi", v.fromNaviBookmark).toString())
        }.apply()

    /** 마지막으로 받은 오늘 추천 출발 시각(시동 알림 문구용). 날짜가 다르면 무시 */
    fun cacheDeparture(dt: LocalDateTime) = prefs.edit().putString(K_DEPARTURE, dt.toString()).apply()

    fun cachedDepartureFor(date: LocalDate): LocalDateTime? =
        prefs.getString(K_DEPARTURE, null)?.let { runCatching { LocalDateTime.parse(it) }.getOrNull() }?.takeIf { it.toLocalDate() == date }

    /** 시동 알림을 같은 날 여러 번 띄우지 않기 위한 기록 */
    var lastNotifiedDate: LocalDate?
        get() = prefs.getString(K_NOTIFIED, null)?.let { runCatching { LocalDate.parse(it) }.getOrNull() }
        set(v) = prefs.edit().putString(K_NOTIFIED, v?.toString()).apply()

    val isSetupDone: Boolean get() = child != null && schedules != null && home != null

    private companion object {
        const val K_CHILD = "child"
        const val K_SCHEDULES = "schedules"
        const val K_HOME = "home"
        const val K_DEPARTURE = "departure_cache"
        const val K_NOTIFIED = "notified_date"
    }
}
