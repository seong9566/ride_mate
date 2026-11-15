package com.example.pickup_hyundai.platform.voice

import androidx.compose.runtime.staticCompositionLocalOf

/**
 * 화면 하단 마이크 바용 음성 입력(SpeechToText). 화면별 짧은 고정 명령만 앱이 직접 처리한다.
 * 자유 문장(질문·복합 요청)은 서버 LLM 대신 Gleo(차량 AI)가 이해하고 Gleo Interface(assets/schema.json)로 앱 함수를 부른다.
 */
class VoiceController(val input: VoiceInput)

val LocalVoice = staticCompositionLocalOf<VoiceController?> { null }
