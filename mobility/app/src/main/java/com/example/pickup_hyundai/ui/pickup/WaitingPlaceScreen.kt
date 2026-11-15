package com.example.pickup_hyundai.ui.pickup

import com.example.pickup_hyundai.map.MapScenes
import com.example.pickup_hyundai.map.MapPanel
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
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
import androidx.compose.material3.Text
import androidx.compose.animation.animateColorAsState
import androidx.compose.animation.core.tween
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.ui.graphics.Color
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.example.pickup_hyundai.core.time.KstFormat
import com.example.pickup_hyundai.domain.model.WaitingPlace
import com.example.pickup_hyundai.domain.model.WaitingPlaceChoice
import com.example.pickup_hyundai.ui.components.MapPlaceholder
import com.example.pickup_hyundai.ui.components.PanelHeader
import com.example.pickup_hyundai.ui.components.PrimaryPanelButton
import com.example.pickup_hyundai.ui.components.WayMeScaffold
import com.example.pickup_hyundai.ui.theme.WayMeColors
import com.example.pickup_hyundai.ui.theme.WayMeExtraColors
import kotlinx.coroutines.delay
import java.time.Duration
import java.time.LocalDateTime

/**
 * V06 대기 장소 추천(출발 추천 시각까지 10분 이상, 차량 출발 전) — 디자인: 3.출발추천시각이 10분이상남았을경우_대기장소추천_차량출발전.png
 * 하단: [바로 시작](대기 없이 픽업 장소로 출발 → 픽업 장소 안내) · [경로 시작](선택한 대기 장소로 안내)
 */
@Composable
fun WaitingPlaceScreen(
    state: WaitingPlaceUiState,
    onSelect: (Int) -> Unit,
    onStartRoute: (WaitingPlace) -> Unit,
    onRetry: () -> Unit,
    /** 대기하지 않고 바로 픽업 장소로 출발 */
    onStartNow: () -> Unit = {},
) {
    val load = state.load
    WayMeScaffold(
        map = { MapPanel(MapScenes.waitingPlaces((load as? LoadState.Content)?.data, state.selectedRank)) },
        panel = {
            PanelHeader("대기 장소", isMock = (load as? LoadState.Content)?.data?.isMock == true)
            when (load) {
                LoadState.Loading -> CenterLoading()
                is LoadState.Error -> CenterError(load.message, onRetry)
                is LoadState.Content -> Body(load.data, state.selectedRank, onSelect, onStartRoute, onStartNow)
            }
        },
    )
}

@Composable
private fun ColumnScope.Body(
    choice: WaitingPlaceChoice,
    selectedRank: Int?,
    onSelect: (Int) -> Unit,
    onStartRoute: (WaitingPlace) -> Unit,
    onStartNow: () -> Unit,
) {
    var now by remember { mutableStateOf(LocalDateTime.now(KstFormat.ZONE)) }
    LaunchedEffect(Unit) { while (true) { delay(30_000); now = LocalDateTime.now(KstFormat.ZONE) } }
    val minutes = Duration.between(now, choice.departureDt).toMinutes()

    Column(
        Modifier
            .weight(1f)
            .padding(horizontal = 28.dp, vertical = 24.dp),
    ) {
        Text("출발까지", color = WayMeColors.TextSecondary, fontSize = 20.sp)
        Row(verticalAlignment = Alignment.Bottom) {
            if (minutes >= 0) {
                Text("$minutes", color = WayMeColors.TextPrimary, fontSize = 80.sp, fontWeight = FontWeight.Bold)
                Spacer(Modifier.width(12.dp))
                Text("분 남았어요", color = WayMeColors.TextPrimary, fontSize = 36.sp, fontWeight = FontWeight.Bold, modifier = Modifier.padding(bottom = 14.dp))
            } else {
                Text("출발 시각이 지났어요", color = WayMeColors.Warning, fontSize = 36.sp, fontWeight = FontWeight.Bold)
            }
        }
        Text("픽업 장소 근처에서 기다릴 곳을 골랐어요", color = WayMeColors.TextPrimary, fontSize = 22.sp)
        Spacer(Modifier.height(20.dp))

        if (choice.places.isEmpty()) {
            Text("근처에 추천할 대기 장소가 없어요", color = WayMeColors.TextSecondary, fontSize = 20.sp)
        } else {
            Column(verticalArrangement = Arrangement.spacedBy(12.dp)) {
                choice.places.take(3).forEach { p ->
                    PlaceCard(p, selected = p.rank == selectedRank, onClick = { onSelect(p.rank) })
                }
            }
        }
        Spacer(Modifier.weight(1f))
        val selected = choice.places.firstOrNull { it.rank == selectedRank }
        Row(horizontalArrangement = Arrangement.spacedBy(16.dp)) {
            // 디자인: 바로 시작(밝은 버튼) · 경로 시작(어두운 버튼)
            androidx.compose.material3.Button(
                onClick = onStartNow,
                modifier = Modifier.weight(1f).height(76.dp),
                shape = RoundedCornerShape(20.dp),
                colors = androidx.compose.material3.ButtonDefaults.buttonColors(containerColor = Color(0xFFE6E7EA), contentColor = Color(0xFF111111)),
            ) { Text("바로 시작", fontSize = 22.sp, fontWeight = FontWeight.Bold) }
            Box(Modifier.weight(1f)) {
                PrimaryPanelButton("경로 시작", onClick = { selected?.let(onStartRoute) }, enabled = selected != null)
            }
        }
    }
}

@Composable
private fun PlaceCard(p: WaitingPlace, selected: Boolean, onClick: () -> Unit) {
    val shape = RoundedCornerShape(20.dp)
    // 선택 시 색이 바뀌는 효과(디자인 "장소선택시 색 바꿈")
    val bg by animateColorAsState(if (selected) WayMeExtraColors.SelectedCard else WayMeColors.Card, tween(220), label = "cardBg")
    val badgeBg by animateColorAsState(if (selected) WayMeColors.Accent else WayMeColors.Avatar, tween(220), label = "badgeBg")
    val borderColor by animateColorAsState(if (selected) WayMeColors.Accent.copy(alpha = 0.6f) else Color.Transparent, tween(220), label = "border")
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .clip(shape)
            .background(bg)
            .border(2.dp, borderColor, shape)
            .clickable(onClick = onClick)
            .padding(horizontal = 24.dp, vertical = 18.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Box(
            Modifier.size(40.dp).clip(CircleShape).background(badgeBg),
            contentAlignment = Alignment.Center,
        ) {
            Text("${p.rank}", color = if (selected) WayMeColors.OnAccent else WayMeColors.TextPrimary, fontSize = 18.sp, fontWeight = FontWeight.Bold)
        }
        Spacer(Modifier.width(20.dp))
        Column {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Text(p.name, color = WayMeColors.TextPrimary, fontSize = 22.sp, fontWeight = FontWeight.Bold)
                if (p.recommended) {
                    Spacer(Modifier.width(10.dp))
                    Text(
                        "추천", color = WayMeColors.Accent, fontSize = 15.sp, fontWeight = FontWeight.Bold,
                        modifier = Modifier.border(1.5.dp, WayMeColors.Accent, RoundedCornerShape(50)).padding(horizontal = 10.dp, vertical = 2.dp),
                    )
                }
            }
            Spacer(Modifier.height(4.dp))
            val sub = listOfNotNull(
                p.driveMin?.let { "차로 ${it}분" },
                p.feeText,
                p.availableSpaces?.let { "주차 가능 ${it}대" },
            ).joinToString("  ·  ")
            if (sub.isNotEmpty()) Text(sub, color = WayMeColors.TextSecondary, fontSize = 18.sp)
        }
    }
}
