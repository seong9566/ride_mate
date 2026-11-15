package com.example.pickup_hyundai.map

import androidx.compose.foundation.Canvas
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.BoxScope
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.compositionLocalOf
import androidx.compose.runtime.remember
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.PathEffect
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.example.pickup_hyundai.BuildConfig
import com.example.pickup_hyundai.domain.route.GeoPoint
import com.example.pickup_hyundai.ui.components.MapPlaceholder
import com.example.pickup_hyundai.ui.theme.WayMeColors

/** 앱 전역 차량 위치(FusedLocation). WayMeRoot에서 제공한다. null = 모름 */
val LocalVehicleLocation = compositionLocalOf<GeoPoint?> { null }

/** 앱 전역: 위치가 에뮬레이터 GPS 시뮬레이션에서 오는 시연 모드인지 */
val LocalDemoLocationMode = compositionLocalOf { false }

private val ChipBg = Color(0xE61E1F22)

/**
 * 지도 패널 진입점: 카카오 지도 + 디자인 오버레이(요약 칩·2D/현재 위치 버튼·범례·시연 표시).
 * 카카오 키가 없으면(local.properties KAKAO_NATIVE_APP_KEY 미설정) 자리표시로 대체.
 */
@Composable
fun BoxScope.MapPanel(scene: MapScene, followVehicle: Boolean = false) {
    if (BuildConfig.KAKAO_NATIVE_APP_KEY.isBlank()) {
        MapPlaceholder("지도 영역 · 카카오맵 키 미설정")
        scene.info?.let { MapInfoChip(it, Modifier.align(Alignment.TopStart).padding(20.dp)) }
        return
    }
    val vehicle = LocalVehicleLocation.current
    val camera = remember { MapCameraController() }
    KakaoMapPanel(scene, vehicle, followVehicle, camera, Modifier.fillMaxSize())

    scene.info?.let { MapInfoChip(it, Modifier.align(Alignment.TopStart).padding(20.dp)) }
    Column(
        Modifier.align(Alignment.TopEnd).padding(20.dp),
        verticalArrangement = Arrangement.spacedBy(12.dp),
    ) {
        MapRoundButton(onClick = camera::toggleTilt) {
            Text(if (camera.tilted) "3D" else "2D", color = WayMeColors.TextPrimary, fontSize = 18.sp, fontWeight = FontWeight.Bold)
        }
        MapRoundButton(onClick = { camera.recenter(vehicle ?: scene.vehicleFallback ?: scene.center) }) { LocateIcon() }
    }
    if (scene.showLegend) MapLegend(Modifier.align(Alignment.BottomEnd).padding(20.dp))
    if (com.example.pickup_hyundai.ui.components.DataBadges.SHOW && (scene.containsDemoData || LocalDemoLocationMode.current)) {
        Text(
            "시연 데이터",
            color = WayMeColors.OnAccent, fontSize = 14.sp, fontWeight = FontWeight.Bold,
            modifier = Modifier.align(Alignment.BottomStart).padding(20.dp).clip(RoundedCornerShape(8.dp))
                .background(WayMeColors.Warning).padding(horizontal = 10.dp, vertical = 4.dp),
        )
    }
}

/** 디자인 지도 좌측 상단 칩: "라벨 | 값" */
@Composable
fun MapInfoChip(info: MapInfo, modifier: Modifier = Modifier) {
    Row(
        modifier.clip(RoundedCornerShape(16.dp)).background(ChipBg).padding(horizontal = 20.dp, vertical = 14.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Text(info.label, color = WayMeColors.TextSecondary, fontSize = 17.sp)
        Box(Modifier.padding(horizontal = 12.dp).width(1.dp).height(18.dp).background(WayMeColors.Divider))
        Text(info.value, color = WayMeColors.TextPrimary, fontSize = 19.sp, fontWeight = FontWeight.Bold)
    }
}

@Composable
private fun MapRoundButton(onClick: () -> Unit, content: @Composable () -> Unit) {
    Box(
        Modifier.size(64.dp).clip(CircleShape).background(ChipBg).clickable(onClick = onClick),
        contentAlignment = Alignment.Center,
    ) { content() }
}

@Composable
private fun LocateIcon() {
    Canvas(Modifier.size(26.dp)) {
        val w = 2.5.dp.toPx()
        drawCircle(Color.White, radius = size.minDimension * 0.32f, style = Stroke(w))
        drawCircle(Color.White, radius = size.minDimension * 0.1f)
        val c = center
        val r = size.minDimension / 2
        drawLine(Color.White, Offset(c.x, 0f), Offset(c.x, c.y - r * 0.64f), w)
        drawLine(Color.White, Offset(c.x, c.y + r * 0.64f), Offset(c.x, size.height), w)
        drawLine(Color.White, Offset(0f, c.y), Offset(c.x - r * 0.64f, c.y), w)
        drawLine(Color.White, Offset(c.x + r * 0.64f, c.y), Offset(size.width, c.y), w)
    }
}

/** 디자인 범례: 차량 경로(실선) / 아이 도보(점선) */
@Composable
private fun MapLegend(modifier: Modifier) {
    Row(
        modifier.clip(RoundedCornerShape(14.dp)).background(ChipBg).padding(horizontal = 18.dp, vertical = 12.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Canvas(Modifier.size(width = 28.dp, height = 6.dp)) {
            drawLine(Color(0xFFC8F135), Offset(0f, size.height / 2), Offset(size.width, size.height / 2), 5.dp.toPx())
        }
        Spacer(Modifier.width(8.dp))
        Text("차량 경로", color = WayMeColors.TextPrimary, fontSize = 15.sp)
        Spacer(Modifier.width(20.dp))
        Canvas(Modifier.size(width = 28.dp, height = 6.dp)) {
            drawLine(
                Color.White, Offset(0f, size.height / 2), Offset(size.width, size.height / 2), 3.dp.toPx(),
                pathEffect = PathEffect.dashPathEffect(floatArrayOf(4.dp.toPx(), 4.dp.toPx())),
            )
        }
        Spacer(Modifier.width(8.dp))
        Text("아이 도보", color = WayMeColors.TextPrimary, fontSize = 15.sp)
    }
}
