package com.example.pickup_hyundai.demo

import androidx.compose.runtime.Composable
import androidx.compose.runtime.compositionLocalOf
import com.example.pickup_hyundai.domain.model.DriveProgress
import com.example.pickup_hyundai.domain.route.GeoPoint
import com.example.pickup_hyundai.platform.location.LocationUiState
import com.example.pickup_hyundai.platform.navi.NaviUiState
import com.example.pickup_hyundai.ui.pickup.LoadState

val LocalLocationState = compositionLocalOf { LocationUiState() }
val LocalNaviState = compositionLocalOf { NaviUiState() }

/**
 * 화면 데이터의 남은 시간·거리를 실시간 값으로 바꾼다.
 * 우선순위: NaviHelper 남은 시간 → 차량 위치 + 경로 투영. 둘 다 없으면 원래(Mock) 값을 유지한다.
 */
@Composable
fun <T> LoadState<T>.withLiveProgress(route: List<GeoPoint>, apply: (T, DriveProgress) -> T): LoadState<T> {
    if (this !is LoadState.Content) return this
    val live = LiveDriveTracker.compute(LocalLocationState.current, LocalNaviState.current, route)
    return if (live.source == ProgressSource.NONE) this else LoadState.Content(apply(data, live.progress))
}
