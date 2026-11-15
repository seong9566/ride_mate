package com.example.pickup_hyundai.ui.pickup

import com.example.pickup_hyundai.domain.voice.VoiceCommand
import com.example.pickup_hyundai.ui.components.VoiceCommandBar
import com.example.pickup_hyundai.map.MapScenes
import com.example.pickup_hyundai.map.MapPanel
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.ColumnScope
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.example.pickup_hyundai.core.time.KstFormat
import com.example.pickup_hyundai.domain.model.DeparturePlan
import com.example.pickup_hyundai.ui.components.MapPlaceholder
import com.example.pickup_hyundai.ui.components.PanelHeader
import com.example.pickup_hyundai.ui.components.PrimaryPanelButton
import com.example.pickup_hyundai.ui.components.WayMeScaffold
import com.example.pickup_hyundai.ui.theme.WayMeColors
import java.time.Duration
import java.time.LocalDateTime

/**
 * V05 오늘의 픽업 출발 전 — 디자인: 픽업 5단계/3.오늘의픽업 출발전.png
 * "경로 시작"은 기어가 P단으로 확인된 경우에만 활성화(디자인 하단 문구).
 */
@Composable
fun DepartureScreen(
    state: LoadState<DeparturePlan>,
    isParked: Boolean,
    onStartRoute: (DeparturePlan) -> Unit,
    onRetry: () -> Unit,
) {
    WayMeScaffold(
        map = { MapPanel(MapScenes.overview()) },
        panel = {
            PanelHeader("오늘의 픽업", isMock = (state as? LoadState.Content)?.data?.isMock == true)
            when (state) {
                LoadState.Loading -> CenterLoading()
                is LoadState.Error -> CenterError(state.message, onRetry)
                is LoadState.Content -> Body(state.data, isParked, onStartRoute)
            }
        },
    )
}

@Composable
private fun ColumnScope.Body(plan: DeparturePlan, isParked: Boolean, onStartRoute: (DeparturePlan) -> Unit) {
    val now = LocalDateTime.now(KstFormat.ZONE)
    Column(
        Modifier
            .weight(1f)
            .padding(horizontal = 28.dp, vertical = 24.dp),
    ) {
        Text("추천 출발 시각", color = WayMeColors.TextSecondary, fontSize = 20.sp)
        Text(KstFormat.hm(plan.departureDt.toLocalTime()), color = WayMeColors.TextPrimary, fontSize = 80.sp, fontWeight = FontWeight.Bold)
        val remain = KstFormat.remaining(Duration.between(now, plan.departureDt))
        val arrival = plan.arrivalDt?.let { KstFormat.hm(it.toLocalTime()) }
        Text(
            // 디자인: 출발 시각이 됐거나 지났으면 "지금 출발하면 HH:mm에 도착해요"
            when {
                remain == null && arrival != null -> "지금 출발하면 ${arrival}에 도착해요"
                remain == null -> "지금 출발해 주세요"
                arrival != null -> "$remain 뒤 출발하면 ${arrival}에 도착해요"
                else -> "$remain 뒤 출발해요"
            },
            color = WayMeColors.TextPrimary,
            fontSize = 22.sp,
        )
        Spacer(Modifier.height(28.dp))
        Column(
            Modifier
                .fillMaxWidth()
                .clip(RoundedCornerShape(20.dp))
                .background(WayMeColors.Card)
                .padding(24.dp),
        ) {
            Text("오늘 경로", color = WayMeColors.TextSecondary, fontSize = 17.sp)
            Spacer(Modifier.height(12.dp))
            RouteStop(plan.originName, sub = null, highlighted = false)
            val sub = buildList {
                add("${plan.academyName} ${KstFormat.hm(plan.endTime)} 하원")
                plan.walkMin?.let { add("아이 도보 ${it}분") }
            }.joinToString("  ·  ")
            RouteStop(plan.pickupName, sub = sub, highlighted = true)
            RouteStop("집", sub = null, highlighted = false, last = true)
            Spacer(Modifier.height(16.dp))
            HorizontalDivider(color = WayMeColors.Divider)
            Spacer(Modifier.height(16.dp))
            Row(Modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically) {
                Text(plan.totalMin?.let { "총 약 ${it}분" } ?: "총 소요 계산 중", color = WayMeColors.TextPrimary, fontSize = 22.sp, fontWeight = FontWeight.Bold)
                Spacer(Modifier.weight(1f))
                plan.homeArrivalTime?.let { Text("집 도착 약 ${KstFormat.hm(it)}", color = WayMeColors.TextSecondary, fontSize = 18.sp) }
            }
        }
        Spacer(Modifier.weight(1f))
        // 음성 "출발": 주행 중에도 말로 경로를 시작할 수 있다(버튼은 P단에서만)
        VoiceCommandBar(screen = "DEPARTURE", idleText = "\"출발\"이라고 말해 주세요") { cmd ->
            if (cmd == VoiceCommand.DEPART || cmd == VoiceCommand.YES) { onStartRoute(plan); true } else false
        }
        Spacer(Modifier.height(16.dp))
        PrimaryPanelButton("경로 시작", onClick = { onStartRoute(plan) }, enabled = isParked)
        Spacer(Modifier.height(16.dp))
        Text(
            "정차(P) 중에만 누를 수 있어요 · 주행 중엔 \"출발\"이라고 말해 주세요",
            color = WayMeColors.TextSecondary, fontSize = 16.sp,
            modifier = Modifier.align(Alignment.CenterHorizontally),
        )
    }
}
