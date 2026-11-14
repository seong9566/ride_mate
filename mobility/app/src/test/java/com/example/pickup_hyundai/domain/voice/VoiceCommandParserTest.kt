package com.example.pickup_hyundai.domain.voice

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

class VoiceCommandParserTest {
    private fun p(t: String) = VoiceCommandParser.parse(t)

    @Test fun depart() { assertEquals(VoiceCommand.DEPART, p("출발")); assertEquals(VoiceCommand.DEPART, p("지금 출발해")) }
    @Test fun send() = assertEquals(VoiceCommand.SEND, p("보내 줘."))
    @Test fun later_beatsSend() { assertEquals(VoiceCommand.LATER, p("나중에")); assertEquals(VoiceCommand.LATER, p("안 보내")) }
    @Test fun yes_exactOnly() { assertEquals(VoiceCommand.YES, p("응")); assertNull(p("어디야")) }
    @Test fun otherPlace() = assertEquals(VoiceCommand.OTHER_PLACE, p("다른 곳"))
    @Test fun remind() = assertEquals(VoiceCommand.REMIND, p("다시 알려줘"))
    @Test fun boarded() = assertEquals(VoiceCommand.BOARDED, p("아이 탔어"))
    @Test fun negation_isNotCommand() { assertNull(p("출발하지 마")); assertNull(p("취소")) }
    @Test fun unknown_goesToBackend() = assertNull(p("학원 앞 주차 돼?"))
}
