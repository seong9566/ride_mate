package com.example.pickup_hyundai.domain.voice

/** 앱이 바로 처리하는 짧은 음성 명령(디자인 안내 문구 기준). 그 밖의 말은 Gleo(차량 AI)가 Gleo Interface로 처리한다 */
enum class VoiceCommand {
    /** "출발", "지금 출발", "가자" */
    DEPART,
    /** "보내", "보내줘" */
    SEND,
    /** "나중에", "안 보내" */
    LATER,
    /** "응", "네", "좋아" */
    YES,
    /** "다른 곳", "다른 데" */
    OTHER_PLACE,
    /** "다시 알려줘" */
    REMIND,
    /** "탔어", "탑승 완료" */
    BOARDED,
}

/** 로컬 명령 해석(순수 Kotlin). 부정 표현("출발하지 마", "취소")은 명령으로 보지 않는다 */
object VoiceCommandParser {
    private val NEGATIONS = listOf("하지마", "하지말", "말고", "취소", "아니")
    private val YES_WORDS = setOf("응", "어", "네", "예", "좋아", "그래", "오케이", "ok")

    fun parse(text: String): VoiceCommand? {
        val t = text.lowercase().replace(Regex("[\\s.,!?~'\"]"), "")
        if (t.isEmpty()) return null
        if (t in YES_WORDS) return VoiceCommand.YES
        if (listOf("나중에", "다음에", "안보내", "보내지마").any { it in t }) return VoiceCommand.LATER
        if (NEGATIONS.any { it in t }) return null
        return when {
            listOf("다른곳", "다른데", "다른장소").any { it in t } -> VoiceCommand.OTHER_PLACE
            listOf("다시알려", "다시알림", "한번더알려").any { it in t } -> VoiceCommand.REMIND
            listOf("보내").any { it in t } -> VoiceCommand.SEND
            listOf("탔어", "탔다", "탑승완료", "태웠어").any { it in t } -> VoiceCommand.BOARDED
            listOf("출발", "가자", "지금가").any { it in t } -> VoiceCommand.DEPART
            else -> null
        }
    }
}
