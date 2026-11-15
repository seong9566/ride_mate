package com.example.pickup_hyundai.domain

import com.example.pickup_hyundai.domain.StopRules.ChildStatus
import com.example.pickup_hyundai.domain.StopRules.TimerPhase
import com.example.pickup_hyundai.domain.model.DeliveryStep
import com.example.pickup_hyundai.domain.model.LinkDelivery
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test
import java.time.LocalDateTime

class StopRulesTest {
    private val t0 = LocalDateTime.of(2026, 11, 16, 22, 2, 0)

    @Test fun phaseBoundaries() {
        assertEquals(TimerPhase.NORMAL, StopRules.phase(31))
        assertEquals(TimerPhase.WARNING, StopRules.phase(30))
        assertEquals(TimerPhase.WARNING, StopRules.phase(1))
        assertEquals(TimerPhase.OVER, StopRules.phase(0))
    }

    @Test fun remainingNeverNegative() {
        assertEquals(42, StopRules.remainingSec(t0.plusSeconds(18), t0, 60))
        assertEquals(0, StopRules.remainingSec(t0.plusSeconds(90), t0, 60))
    }

    @Test fun childStatusFollowsDelivery() {
        assertEquals(ChildStatus.NOT_SENT, StopRules.childStatus(LinkDelivery(null, null, null)))
        assertEquals(ChildStatus.NOT_OPENED, StopRules.childStatus(LinkDelivery(t0, null, null)))
        assertEquals(ChildStatus.OPENED, StopRules.childStatus(LinkDelivery(t0, t0, null)))
        assertEquals(ChildStatus.LEAVING, StopRules.childStatus(LinkDelivery(t0, t0, t0)))
    }

    @Test fun deliveryStepsAreCumulative() {
        val d = LinkDelivery(t0, t0, null)
        assertTrue(d.isDone(DeliveryStep.SENT))
        assertTrue(d.isDone(DeliveryStep.OPENED))
        assertFalse(d.isDone(DeliveryStep.LEAVING))
    }
}
