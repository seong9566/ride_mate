package com.example.pickup_hyundai.platform.vehicle

import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class BoardingSignalDetectorTest {

    @Test fun openThenClose_isCandidate() {
        val d = BoardingSignalDetector()
        assertFalse(d.onDoorChanged(RearDoor.LEFT, isOpen = true))
        assertTrue(d.onDoorChanged(RearDoor.LEFT, isOpen = false))
    }

    @Test fun closeWithoutOpen_isNotCandidate() {
        val d = BoardingSignalDetector()
        assertFalse(d.onDoorChanged(RearDoor.RIGHT, isOpen = false))
    }

    @Test fun candidateFiresOncePerOpening() {
        val d = BoardingSignalDetector()
        d.onDoorChanged(RearDoor.LEFT, true)
        assertTrue(d.onDoorChanged(RearDoor.LEFT, false))
        assertFalse(d.onDoorChanged(RearDoor.LEFT, false))
    }

    @Test fun doorsAreTrackedIndependently() {
        val d = BoardingSignalDetector()
        d.onDoorChanged(RearDoor.LEFT, true)
        assertFalse(d.onDoorChanged(RearDoor.RIGHT, false))
        assertTrue(d.onDoorChanged(RearDoor.LEFT, false))
    }
}
