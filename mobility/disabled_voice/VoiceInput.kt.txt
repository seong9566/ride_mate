package com.example.pickup_hyundai.platform.voice

import ai.pleos.playground.stt.SpeechToText
import ai.pleos.playground.stt.constant.ErrorType
import ai.pleos.playground.stt.constant.Mode
import ai.pleos.playground.stt.listener.OnInitializeListener
import ai.pleos.playground.stt.listener.ResultListener
import android.content.Context
import android.util.Log
import kotlinx.coroutines.flow.MutableSharedFlow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharedFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asSharedFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import java.util.Locale

data class VoiceUiState(
    /** null = 아직 초기화 전, false = 이 차에서 쓸 수 없음 */
    val available: Boolean? = null,
    val listening: Boolean = false,
    /** 인식 중간 결과(completed=false) */
    val partial: String? = null,
    /** 음량(SDK onUpdatedRms, dB 아님) — 듣는 중 표시용 */
    val rms: Double = 0.0,
    val mode: Mode? = null,
    val error: String? = null,
)

/**
 * Pleos SpeechToText SDK(3.3.1) 래퍼: 차량 마이크 음성 → 텍스트.
 * 근거: document.pleos.ai connect-sdk-pleos/SpeechToText (initialization-and-release, result-listeners, start-and-stop, set-locale, error-code)
 * - 권한: pleos.car.permission.STT_SERVICE
 * - 모드: 먼저 ON_DEVICE(앱 등록·네트워크 불필요). 초기화 실패·한국어 미지원이면 HYBRID(서버 우선, 앱 등록 필요)로 한 번 전환.
 * - SDK는 글자로 바꾸기만 한다(명령 실행 없음). 짧은 명령은 VoiceCommandParser, 자유 문장은 Gleo Interface가 처리한다.
 * - ResultListener 패키지는 문서 예시에 import가 없어 OnInitializeListener와 같은 패키지로 가정 → Gradle Sync 후 SDK 소스로 확인 필요.
 */
class VoiceInput(context: Context) {

    private val appContext = context.applicationContext
    private val _state = MutableStateFlow(VoiceUiState())
    val state: StateFlow<VoiceUiState> = _state.asStateFlow()

    /** 최종 인식 문장(completed=true)만 내보낸다 */
    private val _finals = MutableSharedFlow<String>(extraBufferCapacity = 4)
    val finals: SharedFlow<String> = _finals.asSharedFlow()

    private var stt: SpeechToText? = null
    private var mode: Mode = Mode.ON_DEVICE
    private var initialized = false
    private var initializing = false
    private var pendingListen = false
    private var triedFallback = false

    private val listener = object : ResultListener {
        override fun onReady() = Unit
        override fun onStartedRecognition() {
            _state.update { it.copy(listening = true, partial = null, error = null) }
        }
        override fun onUpdated(stt: String, completed: Boolean) {
            if (completed) {
                _state.update { it.copy(partial = stt) }
                if (stt.isNotBlank()) _finals.tryEmit(stt.trim())
            } else {
                _state.update { it.copy(partial = stt) }
            }
        }
        override fun onUpdatedEpdData(on: Long, off: Long) = Unit
        override fun onUpdatedRms(rms: Double) { _state.update { it.copy(rms = rms) } }
        override fun onEndedRecognition() { _state.update { it.copy(listening = false, rms = 0.0) } }
        override fun onError(errorType: ErrorType) {
            Log.w(TAG, "STT error=$errorType mode=$mode")
            _state.update { it.copy(listening = false, rms = 0.0, error = message(errorType)) }
            if (mode == Mode.ON_DEVICE && errorType in FALLBACK_ERRORS) fallbackToHybrid()
        }
    }

    /** 마이크 버튼: 듣는 중이면 멈추고, 아니면 듣기 시작 */
    fun toggle() = if (_state.value.listening) stop() else listen()

    fun listen() {
        if (!initialized) {
            pendingListen = true
            ensureInitialized()
            return
        }
        runCatching { stt?.request() }.onFailure { e -> fail("request", e) }
    }

    fun stop() {
        pendingListen = false
        if (initialized) runCatching { stt?.stop() }
        _state.update { it.copy(listening = false, rms = 0.0) }
    }

    fun release() {
        stop()
        stt?.let { s -> runCatching { s.removeListener(listener); s.release() } }
        stt = null
        initialized = false
        initializing = false
    }

    private fun ensureInitialized() {
        if (initializing || initialized) return
        initializing = true
        val s = runCatching { SpeechToText(appContext, mode) }.getOrElse { e -> fail("create", e); initializing = false; return }
        stt = s
        s.addListener(listener) // 문서: request() 전에 리스너 등록
        s.initialize(object : OnInitializeListener {
            override fun onSuccess() {
                initializing = false
                initialized = true
                runCatching { s.setLocale(Locale.KOREA) }
                _state.update { it.copy(available = true, mode = mode, error = null) }
                if (pendingListen) { pendingListen = false; listen() }
            }

            override fun onFailure(errorType: ErrorType) {
                initializing = false
                Log.w(TAG, "STT init failed=$errorType mode=$mode")
                if (mode == Mode.ON_DEVICE && !triedFallback) {
                    fallbackToHybrid()
                } else {
                    _state.update { it.copy(available = false, error = message(errorType)) }
                }
            }
        })
    }

    private fun fallbackToHybrid() {
        if (triedFallback) return
        triedFallback = true
        val wasPending = pendingListen
        release()
        mode = Mode.HYBRID
        pendingListen = wasPending
        ensureInitialized()
    }

    private fun fail(step: String, e: Throwable) {
        Log.e(TAG, "STT $step failed", e)
        _state.update { it.copy(listening = false, available = it.available ?: false, error = "음성 인식을 시작하지 못했어요") }
    }

    private fun message(e: ErrorType): String = when (e) {
        ErrorType.PERMISSION_DENIED -> "음성 인식 권한이 없어요"
        ErrorType.MIC_ERROR -> "마이크를 쓸 수 없어요. 마이크 설정을 확인해 주세요"
        ErrorType.NETWORK_ERROR, ErrorType.TIMEOUT -> "연결이 불안정해요. 다시 말해 주세요"
        ErrorType.UNAUTHENTICATED -> "음성 인식 서버 인증이 필요해요(앱 등록)"
        ErrorType.UNAVAILABLE_LANGUAGE -> "한국어 음성 인식을 쓸 수 없어요"
        else -> "음성을 알아듣지 못했어요"
    }

    private companion object {
        const val TAG = "WayMe/Voice"
        /** 차 안 엔진으로 안 될 때 HYBRID로 바꿔 볼 오류 */
        val FALLBACK_ERRORS = setOf(ErrorType.UNAVAILABLE_LANGUAGE, ErrorType.ON_DEVICE_ENGINE_ERROR, ErrorType.NOT_SUPPORTED_API)
    }
}
