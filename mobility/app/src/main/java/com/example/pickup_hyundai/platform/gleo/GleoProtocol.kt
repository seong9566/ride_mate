package com.example.pickup_hyundai.platform.gleo

import android.content.Context
import android.content.Intent
import android.util.Log
import org.json.JSONArray
import org.json.JSONObject
import org.json.JSONTokener

/**
 * Pleos Gleo Interface(앱 연동 가이드) 규격.
 * 근거: document.pleos.ai/docs/pleos-only/app-integration-guide/guide (core-concepts, app-capability, action_callback, examples, deliver-schema)
 * - 앱 기능은 assets/schema.json(함수 최대 3개, 설명은 영어)으로 Gleo에 알린다. Pleos 심사 승인 후 활성화.
 * - Gleo가 함수를 고르면 Intent(action = 함수 이름)로 앱을 부른다.
 *   component=activity → startActivity(MainActivity), component=service → sendBroadcast(GleoActionReceiver)
 * - 앱은 7초 안에 브로드캐스트(APP_ACTION_RESPONSE, extras packageName·response)로 결과를 돌려준다.
 */
object GleoProtocol {
    /** 조회(일반 정보) — service */
    const val FN_GET_PICKUP_STATUS = "GET_PICKUP_STATUS"
    /** 상태 변경(픽업 시작·다음 단계) — activity: "글레오, 오늘 픽업 시작해 줘"에 앱 화면을 띄운다 */
    const val FN_START_PICKUP = "START_PICKUP"
    /** 외부 요청(아이에게 문자 발송) — service */
    const val FN_SEND_PICKUP_MESSAGE = "SEND_PICKUP_MESSAGE"
    val FUNCTIONS = setOf(FN_GET_PICKUP_STATUS, FN_START_PICKUP, FN_SEND_PICKUP_MESSAGE)

    const val EXTRA_ACTION_PARAM = "ACTION_PARAM"
    const val ACTION_RESPONSE = "ai.pleos.playground.intent.action.APP_ACTION_RESPONSE"

    /** Gleo는 7초 뒤 취소(공식 Callback 문서) → 앱 전체 처리 상한 */
    const val RESPONSE_BUDGET_MS = 5_500L
    /** 백엔드·저장소 호출 상한(추천 API가 느릴 때 실패 응답을 보낼 시간을 남긴다) */
    const val BACKEND_BUDGET_MS = 3_000L
    private const val TAG = "WayMe/Gleo"

    /**
     * 파라미터 읽기. 문서가 두 방식을 함께 설명하므로 모두 받는다.
     * ① extra "ACTION_PARAM" = JSON 문자열(예시처럼 따옴표로 한 번 더 감싼 문자열도 허용)
     * ② actionParameters의 키마다 extra 하나(값 타입은 문자열·숫자·불리언 등 그대로)
     * 둘 다 있으면 ①이 우선, ①에 없는 키는 ②로 채운다.
     */
    fun params(intent: Intent): JSONObject {
        val out = JSONObject()
        intent.extras?.let { e ->
            e.keySet().filter { it != EXTRA_ACTION_PARAM }.forEach { k ->
                @Suppress("DEPRECATION")
                val v = e.get(k)
                when (v) {
                    null -> Unit
                    is String, is Boolean, is Int, is Long, is Double, is Float -> out.put(k, v)
                    else -> out.put(k, v.toString())
                }
            }
        }
        intent.getStringExtra(EXTRA_ACTION_PARAM)?.let { raw ->
            val parsed = runCatching {
                when (val v = JSONTokener(raw).nextValue()) {
                    is JSONObject -> v
                    is String -> JSONObject(v)
                    else -> null
                }
            }.getOrNull()
            if (parsed == null) Log.w(TAG, "ACTION_PARAM parse failed")
            parsed?.keys()?.forEach { k -> out.put(k, parsed.get(k)) }
        }
        return out
    }

    /** Gleo에 결과 전달. message는 영어 문장(장소 이름 등은 한국어 가능). 문서: data는 JSON 배열을 담은 문자열 */
    fun respond(context: Context, result: GleoResult) {
        val response = JSONObject().apply {
            put("functionName", result.functionName)
            put("status", result.status.wire)
            put("message", result.message)
            result.mode?.let { put("mode", it) }
            result.data?.let { put("data", it.toString()) }
        }
        context.sendBroadcast(
            Intent(ACTION_RESPONSE)
                .putExtra("packageName", context.packageName)
                .putExtra("response", response.toString())
        )
        // 메시지 본문(장소·아이 관련 정보)은 로그에 남기지 않는다
        Log.d(TAG, "${result.functionName} → ${result.status.wire}")
    }
}

enum class GleoStatus(val wire: String) { SUCCESS("success"), ASKING("asking"), FAIL("fail") }

data class GleoResult(
    val functionName: String,
    val status: GleoStatus,
    /** 영어 완성 문장 */
    val message: String,
    val data: JSONArray? = null,
    /** "voice"(기본) · "beep" · "silent" */
    val mode: String? = null,
)

/**
 * 호출자 정보. Android Intent만으로는 Gleo가 보냈다는 사실이 보장되지 않는다.
 * @param packageName 시스템이 알려 준 보낸 앱(Android 14+ BroadcastReceiver.getSentFromPackage, 보낸 쪽이 공개한 경우만). 모르면 null
 * @param verified packageName이 신뢰 목록(GLEO_TRUSTED_PACKAGES)에 있을 때만 true
 */
data class GleoCaller(val packageName: String?, val verified: Boolean)

data class GleoRequest(
    val functionName: String,
    val params: JSONObject,
    val caller: GleoCaller,
)

/** 앱 화면(WayMeAppContent)이 떠 있는 동안 등록되는 처리기. null이면 기본 처리(저장된 일정 안내) */
fun interface GleoActionHandler {
    suspend fun handle(request: GleoRequest): GleoResult?
}
