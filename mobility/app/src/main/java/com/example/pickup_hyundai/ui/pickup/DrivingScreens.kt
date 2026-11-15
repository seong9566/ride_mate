package com.example.pickup_hyundai.ui.pickup

import com.example.pickup_hyundai.ui.components.VoiceCommandBar
import com.example.pickup_hyundai.map.MapScenes
import com.example.pickup_hyundai.map.MapPanel
import androidx.compose.foundation.Canvas
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.BoxScope
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.ColumnScope
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.Path
import androidx.compose.ui.text.SpanStyle
import androidx.compose.ui.text.buildAnnotatedString
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.withStyle
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.example.pickup_hyundai.core.time.KstFormat
import com.example.pickup_hyundai.domain.model.DriveProgress
import com.example.pickup_hyundai.domain.model.HomeDrive
import com.example.pickup_hyundai.domain.route.GeoPoint
import com.example.pickup_hyundai.domain.model.WaitingDrive
import com.example.pickup_hyundai.ui.components.MapPlaceholder
import com.example.pickup_hyundai.ui.components.DevAdvanceChip
import com.example.pickup_hyundai.ui.components.PanelHeader
import com.example.pickup_hyundai.ui.components.WayMeScaffold
import com.example.pickup_hyundai.ui.theme.WayMeColors
import com.example.pickup_hyundai.ui.theme.WayMeExtraColors

/** V07 대기 장소로 이동 — 디자인: 3.대기장소_주행중(첨부 이미지) */
@Composable
fun WaitingDriveScreen(state: LoadState<WaitingDrive>, onRetry: () -> Unit, onDevAdvance: () -> Unit = {}) {
    val data = (state as? LoadState.Content)?.data
    WayMeScaffold(
        map = {
            MapPanel(MapScenes.toWaiting(data?.place), followVehicle = true)
            data?.let { MapInfoChip("대기 장소까지", it.progress) }
            DevAdvanceChip("대기 장소 도착", onDevAdvance)
        },
        panel = {
            PanelHeader("대기 장소로 이동", isMock = data?.isMock == true)
            when (state) {
                LoadState.Loading -> CenterLoading()
                is LoadState.Error -> CenterError(state.message, onRetry)
                is LoadState.Content -> DrivingBody(
                    label = "대기 장소까지",
                    destinationName = state.data.place.name,
                    progress = state.data.progress,
                    guideText = "대기 장소로 안내하고 있어요",
                    voiceScreen = "WAITING_DRIVE",
                ) {
                    InfoCard(
                        title = state.data.place.feeText?.let { "예상 요금 $it" } ?: "요금 정보 없음",
                        subtitle = "공영주차장  ·  자리가 없으면 다음 후보로 안내해요",
                        highlighted = true,
                    ) { ParkingBadge() }
                }
            }
        },
    )
}

/** V10 집으로 가는 중 — 디자인: 픽업 5단계/5.탑승완료.png */
@Composable
fun HomeDriveScreen(state: LoadState<HomeDrive>, route: List<GeoPoint>, onRetry: () -> Unit, onDevAdvance: () -> Unit = {}) {
    val data = (state as? LoadState.Content)?.data
    WayMeScaffold(
        map = {
            MapPanel(MapScenes.toHome(route), followVehicle = true)
            data?.let { MapInfoChip("집까지", it.progress) }
            DevAdvanceChip("집 도착", onDevAdvance)
        },
        panel = {
            PanelHeader("집으로 가는 중", isMock = data?.isMock == true)
            when (state) {
                LoadState.Loading -> CenterLoading()
                is LoadState.Error -> CenterError(state.message, onRetry)
                is LoadState.Content -> DrivingBody(
                    label = "집까지",
                    destinationName = "집 · ${state.data.homeName}",
                    progress = state.data.progress,
                    guideText = "집으로 안내하고 있어요",
                    voiceScreen = "HOME_DRIVE",
                ) {
                    val sub = listOfNotNull(
                        state.data.boardedAt?.let { KstFormat.hm(it.toLocalTime()) },
                        state.data.boardedPlaceName?.let { "$it 탑승" },
                    ).joinToString(" ")
                    InfoCard(title = "${state.data.childName}이가 탔어요", subtitle = sub.ifEmpty { null }, highlighted = false) {
                        WalkerIcon(Modifier.size(24.dp))
                    }
                }
            }
        },
    )
}

/** 주행 중 공통 본문: 라벨 · 큰 남은 시간 · 목적지 · 정보 카드 · 안내 상태 바 */
@Composable
private fun ColumnScope.DrivingBody(
    label: String,
    destinationName: String,
    progress: DriveProgress,
    guideText: String,
    voiceScreen: String = "DRIVING",
    card: @Composable () -> Unit,
) {
    Column(
        Modifier
            .weight(1f)
            .padding(horizontal = 28.dp, vertical = 24.dp),
    ) {
        Text(label, color = WayMeColors.TextSecondary, fontSize = 20.sp)
        Text(progress.remainMin?.let { "${it}분" } ?: "--분", color = WayMeColors.TextPrimary, fontSize = 80.sp, fontWeight = FontWeight.Bold)
        Row(verticalAlignment = Alignment.CenterVertically) {
            PinIcon(Modifier.size(width = 18.dp, height = 22.dp))
            Spacer(Modifier.width(12.dp))
            Text(destinationName, color = WayMeColors.TextPrimary, fontSize = 26.sp, fontWeight = FontWeight.Bold)
            val chip = listOfNotNull(
                progress.distanceKm?.let { "%.1fkm".format(it) },
                progress.arrivalTime?.let { "${KstFormat.hm(it)} 도착" },
            ).joinToString(" · ")
            if (chip.isNotEmpty()) {
                Spacer(Modifier.width(12.dp))
                Text(
                    chip, color = WayMeColors.TextSecondary, fontSize = 16.sp,
                    modifier = Modifier.clip(RoundedCornerShape(8.dp)).background(WayMeColors.Card).padding(horizontal = 10.dp, vertical = 4.dp),
                )
            }
        }
        Spacer(Modifier.height(24.dp))
        card()
        Spacer(Modifier.weight(1f))
        // 이동 중 화면 하단 음성 아이콘(탭하면 듣기). 길안내 음성 자체는 기본 내비가 수행
        VoiceCommandBar(
            screen = voiceScreen,
            idleText = if (progress.guidanceActive) guideText else "말로 물어볼 수 있어요",
        )
    }
}

@Composable
private fun InfoCard(title: String, subtitle: String?, highlighted: Boolean, icon: @Composable () -> Unit) {
    val shape = RoundedCornerShape(20.dp)
    Row(
        Modifier
            .fillMaxWidth()
            .clip(shape)
            .background(if (highlighted) WayMeExtraColors.SelectedCard else WayMeColors.Card)
            .then(if (highlighted) Modifier.border(1.dp, WayMeColors.Accent.copy(alpha = 0.4f), shape) else Modifier)
            .padding(horizontal = 24.dp, vertical = 20.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Box(Modifier.size(52.dp).clip(CircleShape).background(if (highlighted) Color(0xFF2F3420) else WayMeColors.Avatar), contentAlignment = Alignment.Center) { icon() }
        Spacer(Modifier.width(20.dp))
        Column {
            Text(title, color = WayMeColors.TextPrimary, fontSize = 22.sp, fontWeight = FontWeight.Bold)
            subtitle?.let { Text(it, color = WayMeColors.TextSecondary, fontSize = 18.sp) }
        }
    }
}

@Composable
private fun GuidanceStatusBar(text: String) {
    Row(
        Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(50))
            .border(1.dp, WayMeColors.Divider, RoundedCornerShape(50))
            .padding(horizontal = 20.dp, vertical = 16.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Box(Modifier.size(52.dp).clip(CircleShape).background(WayMeColors.Card), contentAlignment = Alignment.Center) {
            SpeakerIcon(Modifier.size(22.dp))
        }
        Spacer(Modifier.width(20.dp))
        Text(text, color = WayMeColors.TextPrimary, fontSize = 22.sp, fontWeight = FontWeight.Bold)
    }
}

/** 지도 패널 상단 요약 칩: "집까지  22:28 · 6.4km · 18분" */
@Composable
private fun BoxScope.MapInfoChip(label: String, p: DriveProgress) {
    val value = listOfNotNull(
        p.arrivalTime?.let(KstFormat::hm),
        p.distanceKm?.let { "%.1fkm".format(it) },
        p.remainMin?.let { "${it}분" },
    ).joinToString(" · ")
    if (value.isEmpty()) return
    Text(
        buildAnnotatedString {
            withStyle(SpanStyle(color = WayMeColors.TextSecondary)) { append("$label  ") }
            withStyle(SpanStyle(color = WayMeColors.TextPrimary, fontWeight = FontWeight.Bold)) { append(value) }
        },
        fontSize = 18.sp,
        modifier = Modifier
            .align(Alignment.TopStart)
            .padding(20.dp)
            .clip(RoundedCornerShape(14.dp))
            .background(WayMeColors.Panel)
            .padding(horizontal = 18.dp, vertical = 12.dp),
    )
}

@Composable
private fun ParkingBadge() {
    Text("P", color = WayMeColors.Accent, fontSize = 22.sp, fontWeight = FontWeight.Bold)
}

@Composable
private fun PinIcon(modifier: Modifier) {
    Canvas(modifier) {
        val r = size.width / 2
        val path = Path().apply {
            moveTo(size.width / 2, size.height)
            lineTo(size.width * 0.12f, r * 1.3f)
            lineTo(size.width * 0.88f, r * 1.3f)
            close()
        }
        drawCircle(WayMeColors.Accent, radius = r, center = Offset(r, r))
        drawPath(path, WayMeColors.Accent)
        drawCircle(WayMeColors.Panel, radius = r * 0.38f, center = Offset(r, r))
    }
}

@Composable
private fun SpeakerIcon(modifier: Modifier) {
    Canvas(modifier) {
        val c = WayMeColors.TextPrimary
        val path = Path().apply {
            moveTo(0f, size.height * 0.35f); lineTo(size.width * 0.25f, size.height * 0.35f)
            lineTo(size.width * 0.5f, size.height * 0.12f); lineTo(size.width * 0.5f, size.height * 0.88f)
            lineTo(size.width * 0.25f, size.height * 0.65f); lineTo(0f, size.height * 0.65f); close()
        }
        drawPath(path, c)
        val w = 2.dp.toPx()
        drawArc(c, -45f, 90f, false, Offset(size.width * 0.45f, size.height * 0.3f), androidx.compose.ui.geometry.Size(size.width * 0.3f, size.height * 0.4f), style = androidx.compose.ui.graphics.drawscope.Stroke(w))
        drawArc(c, -45f, 90f, false, Offset(size.width * 0.45f, size.height * 0.15f), androidx.compose.ui.geometry.Size(size.width * 0.55f, size.height * 0.7f), style = androidx.compose.ui.graphics.drawscope.Stroke(w))
    }
}
