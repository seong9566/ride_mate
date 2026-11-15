package com.example.pickup_hyundai.data.local

import android.content.Context
import com.example.pickup_hyundai.domain.model.PickupRecord
import org.json.JSONArray
import org.json.JSONObject
import java.io.File

/**
 * 픽업 기록 로컬 저장소(V11). 디자인 문구: "기록은 이 차에만 저장되고 서버로 보내지 않아요".
 * 앱 전용 내부 저장소(filesDir)에 JSON으로 보관하며 네트워크로 전송하지 않는다.
 * 같은 날짜 기록은 덮어써 중복 저장을 막는다(08_USER_FLOW §4-8).
 */
class LocalRecordStore(context: Context) {

    private val file = File(context.applicationContext.filesDir, "pickup_records.json")
    private val prefs = context.applicationContext.getSharedPreferences("wayme_settings", Context.MODE_PRIVATE)

    var autoSave: Boolean
        get() = prefs.getBoolean(KEY_AUTO_SAVE, false)
        set(value) { prefs.edit().putBoolean(KEY_AUTO_SAVE, value).apply() }

    @Synchronized
    fun save(record: PickupRecord) {
        val arr = readArray()
        val kept = JSONArray()
        for (i in 0 until arr.length()) {
            val o = arr.getJSONObject(i)
            if (o.optString("date") != record.date.toString()) kept.put(o)
        }
        kept.put(record.toJson())
        // 최근 30건만 보관(불필요한 데이터 축적 방지)
        val trimmed = JSONArray()
        val start = (kept.length() - MAX_RECORDS).coerceAtLeast(0)
        for (i in start until kept.length()) trimmed.put(kept.get(i))
        file.writeText(trimmed.toString())
    }

    @Synchronized
    fun count(): Int = readArray().length()

    private fun readArray(): JSONArray =
        runCatching { if (file.exists()) JSONArray(file.readText()) else JSONArray() }.getOrDefault(JSONArray())

    // 좌표는 저장하지 않는다(위치 데이터 최소 보관 — 심사 가이드 §7)
    private fun PickupRecord.toJson() = JSONObject().apply {
        put("date", date.toString())
        put("departedAt", departedAt?.toString())
        put("departurePlaceName", departurePlaceName)
        put("boardedAt", boardedAt?.toString())
        put("boardedPlaceName", boardedPlaceName)
        put("homeArrivedAt", homeArrivedAt?.toString())
        put("totalMin", totalMin)
        put("waitMin", waitMin)
        put("loopCount", loopCount)
    }

    private companion object {
        const val KEY_AUTO_SAVE = "auto_save_record"
        const val MAX_RECORDS = 30
    }
}
