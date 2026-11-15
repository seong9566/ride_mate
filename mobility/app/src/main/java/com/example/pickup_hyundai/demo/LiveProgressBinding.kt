package com.example.pickup_hyundai.demo

import android.os.SystemClock
import androidx.compose.runtime.Composable
import androidx.compose.runtime.compositionLocalOf
import androidx.compose.runtime.getValue
import androidx.compose.runtime.produceState
import com.example.pickup_hyundai.domain.model.DriveProgress
import com.example.pickup_hyundai.domain.route.GeoPoint
import com.example.pickup_hyundai.platform.location.LocationUiState
import com.example.pickup_hyundai.platform.navi.NaviUiState
import com.example.pickup_hyundai.ui.pickup.LoadState
import kotlinx.coroutines.delay

val LocalLocationState = compositionLocalOf { LocationUiState() }
val LocalNaviState = compositionLocalOf { NaviUiState() }

@Composable
fun liveDriveProgress(route: List<GeoPoint>): LiveProgress {
    val navi = LocalNaviState.current
    val changedAt = navi.remainChangedAtMs
    val expiryTick by produceState(0L, changedAt) {
        if (changedAt != null) {
            val expiresAt = changedAt + NAVI_ETA_TIMEOUT_MS
            delay((expiresAt - SystemClock.elapsedRealtime()).coerceAtLeast(0))
            value = expiresAt
        }
    }
    return LiveDriveTracker.compute(LocalLocationState.current, navi, route, maxOf(SystemClock.elapsedRealtime(), expiryTick))
}

fun <T> LoadState<T>.withLiveProgress(live: LiveProgress, apply: (T, DriveProgress) -> T): LoadState<T> {
    if (this !is LoadState.Content) return this
    return if (live.source == ProgressSource.NONE) this else LoadState.Content(apply(data, live.progress))
}
