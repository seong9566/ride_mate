package com.example.pickup_hyundai.ui.pickup

import com.example.pickup_hyundai.map.MapScenes
import com.example.pickup_hyundai.map.MapPanel
import androidx.compose.foundation.Canvas
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
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.example.pickup_hyundai.core.time.KstFormat
import com.example.pickup_hyundai.domain.model.PickupSpotCandidate
import com.example.pickup_hyundai.domain.model.PickupSpotChoice
import com.example.pickup_hyundai.domain.model.SpotTag
import com.example.pickup_hyundai.ui.components.MapPlaceholder
import com.example.pickup_hyundai.ui.components.PanelHeader
import com.example.pickup_hyundai.ui.components.PrimaryPanelButton
import com.example.pickup_hyundai.ui.components.WayMeScaffold
import com.example.pickup_hyundai.ui.theme.WayMeColors
import com.example.pickup_hyundai.ui.theme.WayMeExtraColors

/**
 * V04 픽업 장소 추천 — 디자인: 2.픽업 장소 추천.png / 2.픽업 장소추천_장소선택시 색 바꿈.png
 * 카드를 탭하면 강조되고, 선택 후 "이곳으로 정하고 경로 시작"이 활성화된다.
 */
@Composable
fun PickupSpotScreen(
    state: PickupSpotUiState,
    onBack: () -> Unit,
    onSelect: (Int) -> Unit,
    onConfirm: (PickupSpotCandidate) -> Unit,
    onCustomSpot: () -> Unit,
    onRetry: () -> Unit,
) {
    val load = state.load
    WayMeScaffold(
        map = { MapPanel(MapScenes.pickupSpots((load as? LoadState.Content)?.data, state.selectedRank)) },
        panel = {
            PanelHeader("픽업 장소 선택", isMock = (load as? LoadState.Content)?.data?.isMock == true, onBack = onBack)
            when (load) {
                LoadState.Loading -> CenterLoading()
                is LoadState.Error -> CenterError(load.message, onRetry)
                is LoadState.Content -> Body(load.data, state.selectedRank, onSelect, onConfirm, onCustomSpot)
            }
        },
    )
}

@Composable
private fun ColumnScope.Body(
    choice: PickupSpotChoice,
    selectedRank: Int?,
    onSelect: (Int) -> Unit,
    onConfirm: (PickupSpotCandidate) -> Unit,
    onCustomSpot: () -> Unit,
) {
    Column(
        Modifier
            .weight(1f)
            .padding(horizontal = 28.dp, vertical = 24.dp),
    ) {
        Text("오늘은 여기서 만나는 걸 추천해요", color = WayMeColors.TextPrimary, fontSize = 28.sp, fontWeight = FontWeight.Bold)
        choice.basisText?.let { Text(it, color = WayMeColors.TextSecondary, fontSize = 18.sp) }
        Spacer(Modifier.height(16.dp))

        if (choice.candidates.isEmpty()) {
            Text("오늘은 추천할 수 있는 픽업 장소가 없어요", color = WayMeColors.TextSecondary, fontSize = 20.sp)
        } else {
            Column(verticalArrangement = Arrangement.spacedBy(12.dp)) {
                // 08_USER_FLOW §2.1: 실제 데이터가 있는 만큼만(최대 3개) 노출
                choice.candidates.take(3).forEach { c ->
                    SpotCard(c, selected = c.rank == selectedRank, onClick = { onSelect(c.rank) })
                }
            }
        }

        if (choice.departureDt != null) {
            Spacer(Modifier.height(20.dp))
            Row(verticalAlignment = Alignment.CenterVertically) {
                ClockIcon()
                Spacer(Modifier.width(16.dp))
                Column {
                    val arrival = choice.arrivalDt?.let { " · ${KstFormat.hm(it.toLocalTime())} 도착" } ?: ""
                    Text("${KstFormat.hm(choice.departureDt.toLocalTime())} 출발$arrival", color = WayMeColors.TextPrimary, fontSize = 22.sp, fontWeight = FontWeight.Bold)
                    Text("아이에게도 이 장소로 안내해요", color = WayMeColors.TextSecondary, fontSize = 17.sp)
                }
            }
        }

        Spacer(Modifier.weight(1f))
        val selected = choice.candidates.firstOrNull { it.rank == selectedRank }
        PrimaryPanelButton("이곳으로 정하고 경로 시작", onClick = { selected?.let(onConfirm) }, enabled = selected != null)
        Spacer(Modifier.height(12.dp))
        Row(
            Modifier
                .align(Alignment.CenterHorizontally)
                .clip(RoundedCornerShape(12.dp))
                .clickable(onClick = onCustomSpot)
                .padding(horizontal = 12.dp, vertical = 10.dp),
        ) {
            Text("원하는 곳이 없나요?  ", color = WayMeColors.TextSecondary, fontSize = 18.sp)
            Text("직접 장소 입력하기 〉", color = WayMeExtraColors.Link, fontSize = 18.sp, fontWeight = FontWeight.Bold)
        }
    }
}

@Composable
private fun SpotCard(c: PickupSpotCandidate, selected: Boolean, onClick: () -> Unit) {
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
            Modifier
                .size(40.dp)
                .clip(CircleShape)
                .background(badgeBg),
            contentAlignment = Alignment.Center,
        ) {
            Text("${c.rank}", color = if (selected) WayMeColors.OnAccent else WayMeColors.TextPrimary, fontSize = 18.sp, fontWeight = FontWeight.Bold)
        }
        Spacer(Modifier.width(20.dp))
        Column {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Text(c.name, color = WayMeColors.TextPrimary, fontSize = 22.sp, fontWeight = FontWeight.Bold)
                c.tag?.let { Spacer(Modifier.width(10.dp)); Tag(it) }
            }
            Spacer(Modifier.height(4.dp))
            val sub = listOfNotNull(c.walkMin?.let { "아이 도보 ${it}분" }, c.reason).joinToString("  ·  ")
            if (sub.isNotEmpty()) Text(sub, color = WayMeColors.TextSecondary, fontSize = 18.sp)
        }
    }
}

@Composable
private fun Tag(tag: SpotTag) {
    val shape = RoundedCornerShape(50)
    when (tag) {
        SpotTag.RECOMMENDED -> Text(
            "추천", color = WayMeColors.Accent, fontSize = 15.sp, fontWeight = FontWeight.Bold,
            modifier = Modifier.border(1.5.dp, WayMeColors.Accent, shape).padding(horizontal = 10.dp, vertical = 2.dp),
        )
        SpotTag.LAST_TIME -> Text(
            "지난번", color = WayMeColors.TextSecondary, fontSize = 15.sp,
            modifier = Modifier.clip(shape).background(WayMeExtraColors.TagNeutral).padding(horizontal = 10.dp, vertical = 2.dp),
        )
    }
}

@Composable
private fun ClockIcon() {
    Box(
        Modifier.size(48.dp).clip(CircleShape).background(WayMeColors.Card),
        contentAlignment = Alignment.Center,
    ) {
        Canvas(Modifier.size(22.dp)) {
            val w = 2.dp.toPx()
            drawCircle(WayMeColors.Accent, radius = size.minDimension / 2 - w / 2, style = Stroke(w))
            drawLine(WayMeColors.Accent, center, Offset(center.x, center.y - size.height * 0.28f), strokeWidth = w)
            drawLine(WayMeColors.Accent, center, Offset(center.x + size.width * 0.22f, center.y + size.height * 0.1f), strokeWidth = w)
        }
    }
}
