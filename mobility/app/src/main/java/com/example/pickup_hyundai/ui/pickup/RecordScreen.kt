package com.example.pickup_hyundai.ui.pickup

import com.example.pickup_hyundai.map.MapScenes
import com.example.pickup_hyundai.map.MapPanel
import androidx.compose.foundation.Canvas
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Box
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
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Switch
import androidx.compose.material3.SwitchDefaults
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.StrokeCap
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.example.pickup_hyundai.core.time.KstFormat
import com.example.pickup_hyundai.domain.model.PickupRecord
import com.example.pickup_hyundai.domain.route.GeoPoint
import com.example.pickup_hyundai.ui.components.MapPlaceholder
import com.example.pickup_hyundai.ui.components.PanelHeader
import com.example.pickup_hyundai.ui.components.PrimaryPanelButton
import com.example.pickup_hyundai.ui.components.WayMeScaffold
import com.example.pickup_hyundai.ui.theme.WayMeColors
import com.example.pickup_hyundai.ui.theme.WayMeExtraColors

/** V11 집 도착·기록 저장 — 디자인: 픽업 5단계/6.집도착_기록저장.png */
@Composable
fun RecordScreen(
    state: RecordUiState,
    route: List<GeoPoint>,
    onAutoSaveChange: (Boolean) -> Unit,
    onSave: () -> Unit,
    onSkip: () -> Unit,
    onRetry: () -> Unit,
) {
    WayMeScaffold(
        map = { MapPanel(MapScenes.toHome(route)) },
        panel = {
            // 기록 요약은 아직 Mock(탑승·도착 시각을 주는 API 미정) → MOCK 표시
            PanelHeader("픽업 완료", isMock = state.load is LoadState.Content)
            when (val load = state.load) {
                LoadState.Loading -> CenterLoading()
                is LoadState.Error -> CenterError(load.message, onRetry)
                is LoadState.Content -> Body(load.data, state, onAutoSaveChange, onSave, onSkip)
            }
        },
    )
}

@Composable
private fun ColumnScope.Body(
    r: PickupRecord,
    state: RecordUiState,
    onAutoSaveChange: (Boolean) -> Unit,
    onSave: () -> Unit,
    onSkip: () -> Unit,
) {
    Column(
        Modifier
            .weight(1f)
            .padding(horizontal = 28.dp, vertical = 24.dp),
    ) {
        Row(verticalAlignment = Alignment.CenterVertically) {
            Box(Modifier.size(44.dp).clip(CircleShape).background(Color(0xFF1F3A2A)), contentAlignment = Alignment.Center) {
                Canvas(Modifier.size(18.dp)) {
                    val w = 2.5.dp.toPx()
                    val c = Color(0xFF34C77B)
                    drawLine(c, Offset(0f, size.height * 0.55f), Offset(size.width * 0.38f, size.height * 0.9f), w, cap = StrokeCap.Round)
                    drawLine(c, Offset(size.width * 0.38f, size.height * 0.9f), Offset(size.width, size.height * 0.15f), w, cap = StrokeCap.Round)
                }
            }
            Spacer(Modifier.width(16.dp))
            Text("오늘 픽업 완료!", color = WayMeColors.TextPrimary, fontSize = 30.sp, fontWeight = FontWeight.Bold)
        }
        Spacer(Modifier.height(20.dp))

        Column(
            Modifier
                .fillMaxWidth()
                .clip(RoundedCornerShape(20.dp))
                .background(WayMeColors.Card)
                .padding(24.dp),
        ) {
            Text("집 도착", color = WayMeColors.TextSecondary, fontSize = 17.sp)
            Row(verticalAlignment = Alignment.Bottom) {
                Text(r.homeArrivedAt?.let(KstFormat::hm) ?: "--:--", color = WayMeColors.TextPrimary, fontSize = 40.sp, fontWeight = FontWeight.Bold)
                r.totalMin?.let {
                    Spacer(Modifier.width(12.dp))
                    Text("총 ${it}분", color = WayMeColors.TextSecondary, fontSize = 20.sp, modifier = Modifier.padding(bottom = 6.dp))
                }
            }
            val summary = listOfNotNull(r.waitMin?.let { "대기 ${it}분" }, r.loopCount.takeIf { it > 0 }?.let { "한 바퀴 ${it}번" })
            if (summary.isNotEmpty()) Text(summary.joinToString("  ·  "), color = WayMeColors.TextPrimary, fontSize = 18.sp)
            Spacer(Modifier.height(16.dp))
            HorizontalDivider(color = WayMeColors.Divider)
            Spacer(Modifier.height(16.dp))
            RecordRow("출발", listOfNotNull(r.departedAt?.let(KstFormat::hm), r.departurePlaceName).joinToString("  ·  "))
            Spacer(Modifier.height(8.dp))
            val boardPlace = r.boardedPlaceName?.let { name -> if (r.loopCount > 0) "$name (한 바퀴 ${r.loopCount}번)" else name }
            RecordRow("탑승", listOfNotNull(r.boardedAt?.let(KstFormat::hm), boardPlace).joinToString("  ·  "))
        }
        Spacer(Modifier.height(20.dp))

        val shape = RoundedCornerShape(20.dp)
        Column(
            Modifier
                .fillMaxWidth()
                .clip(shape)
                .background(WayMeExtraColors.SelectedCard)
                .border(1.dp, WayMeColors.Accent.copy(alpha = 0.4f), shape)
                .padding(24.dp),
        ) {
            Text("오늘 기록을 이 차에 저장할까요?", color = WayMeColors.TextPrimary, fontSize = 22.sp, fontWeight = FontWeight.Bold)
            Text("다음 픽업 때 출발 시각과 장소를 더 정확하게 추천해요", color = WayMeColors.TextSecondary, fontSize = 18.sp)
        }
        Spacer(Modifier.height(16.dp))
        Row(Modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically) {
            Text("다음부터 묻지 않고 저장", color = WayMeColors.TextPrimary, fontSize = 20.sp, modifier = Modifier.weight(1f))
            Switch(
                checked = state.autoSave,
                onCheckedChange = onAutoSaveChange,
                colors = SwitchDefaults.colors(checkedTrackColor = WayMeColors.Accent, checkedThumbColor = WayMeColors.OnAccent),
            )
        }
        Text("기록은 이 차에만 저장되고 서버로 보내지 않아요", color = WayMeColors.TextTertiary, fontSize = 16.sp)
        if (state.autoSaved) {
            Spacer(Modifier.height(8.dp))
            Text("자동으로 이 차에 저장했어요", color = WayMeColors.Accent, fontSize = 18.sp)
        }
        state.error?.let {
            Spacer(Modifier.height(8.dp))
            Text(it, color = WayMeColors.Warning, fontSize = 18.sp)
        }
        Spacer(Modifier.weight(1f))
        PrimaryPanelButton(if (state.saving) "저장 중…" else "저장할게요", onSave, enabled = !state.saving)
        Spacer(Modifier.height(8.dp))
        Text(
            "저장 안 함",
            color = WayMeExtraColors.Link, fontSize = 20.sp, fontWeight = FontWeight.Bold,
            modifier = Modifier
                .align(Alignment.CenterHorizontally)
                .clip(RoundedCornerShape(12.dp))
                .clickable(enabled = !state.saving, onClick = onSkip)
                .padding(horizontal = 24.dp, vertical = 12.dp),
        )
    }
}

@Composable
private fun RecordRow(label: String, value: String) {
    Row {
        Text(label, color = WayMeColors.TextSecondary, fontSize = 18.sp, modifier = Modifier.width(110.dp))
        Text(value.ifEmpty { "-" }, color = WayMeColors.TextPrimary, fontSize = 18.sp)
    }
}
