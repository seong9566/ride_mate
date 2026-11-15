package com.example.pickup_hyundai.ui.debug

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.FlowRow
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import com.example.pickup_hyundai.platform.location.LocationUiState
import com.example.pickup_hyundai.platform.navi.NaviUiState
import com.example.pickup_hyundai.platform.vehicle.RearDoorUiState

/**
 * 1단계 SDK 검증 전용 화면(개발용). 3단계에서 디자인 기반 화면으로 교체한다.
 */
@Composable
fun SdkCheckScreen(
    door: RearDoorUiState,
    navi: NaviUiState,
    location: LocationUiState,
    locationPermissionGranted: Boolean,
    onRequestLocationPermission: () -> Unit,
    onRequestRoute: () -> Unit,
    onAddWaypoint: () -> Unit,
    onRequestRouteWithWaypoint: () -> Unit,
    onCancelRoute: () -> Unit,
    onRequestTrip: () -> Unit,
    modifier: Modifier = Modifier,
) {
    Column(
        modifier = modifier
            .fillMaxSize()
            .verticalScroll(rememberScrollState())
            .padding(24.dp),
        verticalArrangement = Arrangement.spacedBy(16.dp),
    ) {
        Text("WAY:ME · Pleos SDK 검증 (개발용)", style = MaterialTheme.typography.headlineSmall)

        Section("Vehicle · 뒷문 (CAR_DOORS)") {
            door.doors.forEach { (d, s) ->
                Text("${d.name}: 지원=${s.supported ?: "?"} · 위치=${s.position ?: "?"} · 닫힘값=${s.closedValue ?: "?"} · 열림=${s.isOpen ?: "?"}")
            }
            Text("탑승 후보 신호(열림→닫힘): ${door.boardingCandidateCount}회")
            door.lastError?.let { Text("오류: $it", color = MaterialTheme.colorScheme.error) }
        }

        Section("NaviHelper (NAVI_ROUTE / NAVI_ROUTE_SEARCH)") {
            Text("Navi Service 설치=${navi.naviServiceInstalled ?: "?"}")
            Text("연결=${navi.connected} · 상태=${navi.navigationStatus ?: "?"} · 경로중=${navi.routing}")
            navi.connectError?.let { Text("연결 실패: $it", color = MaterialTheme.colorScheme.error) }
            Text("남은 시간(DrivingInfo)=${navi.remainSec?.let { "${it}초" } ?: "-"} · 거리=${navi.destinationDistanceM?.let { "${it}m" } ?: "-"}")
            Text("Trip ETA=${navi.tripEtaSec?.let { "${it}초" } ?: "-"} · 거리=${navi.tripDistanceM?.let { "${it}m" } ?: "-"}")
            Text("경유지: ${if (navi.waypoints.isEmpty()) "-" else navi.waypoints.joinToString()}")
            navi.arrived?.let { Text("도착: ${it.name} (${it.distance}m, ${it.duration}초)") }
            Text("마지막 이벤트: ${navi.lastEvent ?: "-"}")
            navi.lastError?.let { Text("오류: $it", color = MaterialTheme.colorScheme.error) }
            FlowRow(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                Button(onClick = onRequestRoute) { Text("테스트 목적지 경로") }
                Button(onClick = onAddWaypoint) { Text("경유지 추가(경로 중)") }
                Button(onClick = onRequestRouteWithWaypoint) { Text("경유지 포함 경로") }
                Button(onClick = onRequestTrip) { Text("ETA 조회") }
                Button(onClick = onCancelRoute) { Text("경로 취소") }
            }
        }

        Section("FusedLocation (FUSED_LOCATION)") {
            if (!locationPermissionGranted) {
                Text("위치 권한이 필요합니다.")
                Button(onClick = onRequestLocationPermission) { Text("권한 요청") }
            } else {
                Text("지원=${location.supported ?: "?"}")
                Text(location.location?.let { "위도 ${it.latitude}, 경도 ${it.longitude}, 방향 ${it.heading}" } ?: "위치 없음")
            }
            location.lastError?.let { Text("오류: $it", color = MaterialTheme.colorScheme.error) }
        }
    }
}

@Composable
private fun Section(title: String, content: @Composable () -> Unit) {
    Card {
        Column(Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(6.dp)) {
            Text(title, style = MaterialTheme.typography.titleMedium)
            content()
        }
    }
}
