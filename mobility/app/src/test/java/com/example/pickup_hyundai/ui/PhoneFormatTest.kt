package com.example.pickup_hyundai.ui

import com.example.pickup_hyundai.ui.components.formatPhone
import com.example.pickup_hyundai.ui.components.maskPhone
import org.junit.Assert.assertEquals
import org.junit.Test

class PhoneFormatTest {
    @Test fun formatsProgressively() {
        assertEquals("010", formatPhone("010"))
        assertEquals("010-1234", formatPhone("0101234"))
        assertEquals("010-1234-5678", formatPhone("01012345678"))
    }

    @Test fun masksMiddle() = assertEquals("010-****-5678", maskPhone("01012345678"))
}
