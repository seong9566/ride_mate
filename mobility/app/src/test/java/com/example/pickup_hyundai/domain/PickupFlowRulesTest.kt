package com.example.pickup_hyundai.domain

import com.example.pickup_hyundai.domain.PickupFlowRules.AfterSpotSelected
import org.junit.Assert.assertEquals
import org.junit.Test
import java.time.LocalDateTime

class PickupFlowRulesTest {
    private val departure = LocalDateTime.of(2026, 11, 16, 21, 29)

    @Test fun moreThan10Min_goesToWaitingPlace() =
        assertEquals(AfterSpotSelected.WAITING_PLACE, PickupFlowRules.afterSpotSelected(departure.minusMinutes(35), departure))

    @Test fun exactly10Min_goesToWaitingPlace() =
        assertEquals(AfterSpotSelected.WAITING_PLACE, PickupFlowRules.afterSpotSelected(departure.minusMinutes(10), departure))

    @Test fun lessThan10Min_goesToBeforeDeparture() =
        assertEquals(AfterSpotSelected.BEFORE_DEPARTURE, PickupFlowRules.afterSpotSelected(departure.minusMinutes(9), departure))

    @Test fun pastDeparture_goesToBeforeDeparture() =
        assertEquals(AfterSpotSelected.BEFORE_DEPARTURE, PickupFlowRules.afterSpotSelected(departure.plusMinutes(5), departure))
}
