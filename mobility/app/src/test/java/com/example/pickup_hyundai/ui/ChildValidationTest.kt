package com.example.pickup_hyundai.ui

import com.example.pickup_hyundai.domain.model.ChildInfoInput
import com.example.pickup_hyundai.ui.setup.SetupViewModel
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test
import java.time.LocalDate

class ChildValidationTest {
    private val today = LocalDate.of(2026, 11, 13)
    private val ok = ChildInfoInput("지민", "01098761234", "20100514")

    @Test fun valid() = assertNull(SetupViewModel.validateChild(ok, today))
    @Test fun blankName() = assertEquals("이름을 입력해 주세요", SetupViewModel.validateChild(ok.copy(name = " "), today))
    @Test fun badPhone() = assertEquals("휴대폰 번호 11자리를 입력해 주세요", SetupViewModel.validateChild(ok.copy(phoneDigits = "0101234"), today))
    @Test fun impossibleDate() = assertEquals("생년월일을 확인해 주세요", SetupViewModel.validateChild(ok.copy(birthDigits = "20100231"), today))
    @Test fun futureDate() = assertEquals("생년월일을 확인해 주세요", SetupViewModel.validateChild(ok.copy(birthDigits = "20300101"), today))
}

class ChildNameRuleTest {
    private val today = java.time.LocalDate.of(2026, 11, 15)
    private fun name(n: String) = SetupViewModel.validateChild(
        com.example.pickup_hyundai.domain.model.ChildInfoInput(n, "01098761234", "20100514"), today,
    )
    private val bad = "이름은 한글·영문만 10자까지 쓸 수 있어요"

    @Test fun koreanAndEnglish() { assertNull(name("지민")); assertNull(name("Kim Jimin")) }
    @Test fun digitsRejected() = assertEquals(bad, name("지민2"))
    @Test fun doubleSpaceRejected() = assertEquals(bad, name("김  지민"))
    @Test fun jamoRejected() = assertEquals(bad, name("ㅈㅁ"))
    @Test fun tooLong() = assertEquals(bad, name("가나다라마바사아자차카"))
}
