package com.example.pickup_hyundai.platform.gleo

import android.content.Context
import com.example.pickup_hyundai.BuildConfig
import com.example.pickup_hyundai.core.time.KstFormat
import com.example.pickup_hyundai.data.local.SetupStore
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.filterNotNull
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.launch
import kotlinx.coroutines.withTimeoutOrNull
import org.json.JSONArray
import org.json.JSONObject
import java.time.LocalDate
import java.time.LocalTime

/**
 * Gleo 요청 처리 공통 경로(리시버·액티비티 모두 사용).
 * 보안(호출자를 믿지 않는 설계):
 *  1) 로그인 확인 — 이 차에 로그인한 계정이 없으면 모든 요청 거절(개인정보 노출 방지)
 *  2) 중복 요청 차단 — 같은 함수·같은 값이 3초 안에 다시 오면 한 번만 처리
 *  3) 요청 유효성 — 화면(WayMeAppContent)이 현재 단계에서 가능한 요청인지 확인
 *  4) 외부 요청(문자 발송) — 호출자가 확인되지 않으면 실행하지 않고 운전자 화면 확인을 거친다
 * 응답: 전체 5.5초 상한(Gleo 7초 취소), 넘으면 fail 응답.
 */
object GleoBridge {
    val handler = MutableStateFlow<GleoActionHandler?>(null)
    private val scope = CoroutineScope(SupervisorJob() + Dispatchers.Main)
    private var lastKey: String? = null
    private var lastAt = 0L

    /** 신뢰 호출자 목록(local.properties GLEO_TRUSTED_PACKAGES, 쉼표 구분). Gleo 패키지명이 공식 확인되면 넣는다 */
    private val trusted: Set<String> = BuildConfig.GLEO_TRUSTED_PACKAGES.split(',').map { it.trim() }.filter { it.isNotEmpty() }.toSet()

    fun caller(packageName: String?) = GleoCaller(packageName, verified = packageName != null && packageName in trusted)

    /**
     * @param waitForUi 액티비티로 호출된 경우(앱을 막 띄움) 화면 처리기가 준비될 때까지 기다린다
     * @param onDone 처리 완료 후(리시버 goAsync 종료 등)
     */
    fun dispatch(context: Context, request: GleoRequest, waitForUi: Boolean, onDone: () -> Unit = {}) {
        val app = context.applicationContext
        scope.launch {
            val result = withTimeoutOrNull(GleoProtocol.RESPONSE_BUDGET_MS) { process(app, request, waitForUi) }
                ?: GleoResult(request.functionName, GleoStatus.FAIL, "RideMate could not finish the request in time. Please check the RideMate screen.")
            runCatching { GleoProtocol.respond(app, result) }
            onDone()
        }
    }

    private suspend fun process(context: Context, request: GleoRequest, waitForUi: Boolean): GleoResult {
        val fn = request.functionName
        val store = SetupStore(context)
        if (!store.loggedIn) {
            return GleoResult(fn, GleoStatus.FAIL, "Please log in to RideMate on this car first.")
        }
        val key = fn + request.params.toString()
        val nowMs = System.currentTimeMillis()
        if (key == lastKey && nowMs - lastAt < DEDUPE_MS) {
            return GleoResult(fn, GleoStatus.FAIL, "The same request was just handled.")
        }
        lastKey = key; lastAt = nowMs

        val h = handler.value ?: if (waitForUi) withTimeoutOrNull(UI_WAIT_MS) { handler.filterNotNull().first() } else null
        return h?.handle(request) ?: handleWithoutUi(store, fn)
    }

    /** 앱 화면이 없을 때: 오늘 일정 조회만 답하고, 상태 변경·외부 요청은 앱에서 픽업을 시작하도록 안내 */
    private fun handleWithoutUi(store: SetupStore, fn: String): GleoResult {
        if (fn != GleoProtocol.FN_GET_PICKUP_STATUS) {
            return GleoResult(fn, GleoStatus.FAIL, "No pickup is in progress. Ask to start today's pickup first.")
        }
        val today = LocalDate.now(KstFormat.ZONE)
        val first = store.schedules?.filter { it.pickupEnabled && it.day == today.dayOfWeek }?.minByOrNull { it.endTime }
            ?: return GleoResult(fn, GleoStatus.SUCCESS, "There is no pickup scheduled for today.")
        if (LocalTime.now(KstFormat.ZONE).isAfter(first.endTime)) {
            return GleoResult(fn, GleoStatus.SUCCESS, "Today's pickup time has already passed.")
        }
        val dep = store.cachedDepartureFor(today)
        val msg = buildString {
            append("Today's pickup is at ${first.academyName}, which ends at ${KstFormat.hm(first.endTime)}.")
            dep?.let { append(" The recommended departure time is ${KstFormat.hm(it.toLocalTime())}.") }
        }
        // 개인정보 최소화: 아이 이름·주소·좌표는 보내지 않는다
        val data = JSONArray().put(JSONObject().apply {
            put("stage", "NOT_STARTED")
            put("academyName", first.academyName)
            put("endTime", KstFormat.hm(first.endTime))
            dep?.let { put("departureTime", KstFormat.hm(it.toLocalTime())) }
        })
        return GleoResult(fn, GleoStatus.SUCCESS, msg, data)
    }

    private const val DEDUPE_MS = 3_000L
    private const val UI_WAIT_MS = 3_000L
}
