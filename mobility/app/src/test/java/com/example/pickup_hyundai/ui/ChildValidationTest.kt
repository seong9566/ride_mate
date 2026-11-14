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
