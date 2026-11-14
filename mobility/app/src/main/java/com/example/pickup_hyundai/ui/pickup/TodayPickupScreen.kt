package com.example.pickup_hyundai.ui.pickup

import com.example.pickup_hyundai.map.MapScenes
import com.example.pickup_hyundai.map.MapPanel
import androidx.compose.foundation.Canvas
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.ColumnScope
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.example.pickup_hyundai.core.time.KstFormat
import com.example.pickup_hyundai.domain.model.TodayPickupDetail
import com.example.pickup_hyundai.ui.components.ChildAvatar
import com.example.pickup_hyundai.ui.components.MapPlaceholder
import com.example.pickup_hyundai.ui.components.PanelHeader
import com.example.pickup_hyundai.ui.components.PrimaryPanelButton
import com.example.pickup_hyundai.ui.components.WayMeScaffold
import com.example.pickup_hyundai.ui.theme.WayMeColors
import java.time.Duration
import java.time.LocalDateTime

/** V03 오늘 하원 일정 — 디자인: 픽업 5단계/1.오늘 하원 일정.png */
@Composable
fun TodayPickupScreen(
    state: LoadState<TodayPickupDetail>,
    onChangeHome: () -> Unit,
    onChoosePickupSpot: () -> Unit,
    onRetry: () -> Unit,
) {
    WayMeScaffold(
        map = { MapPanel(MapScenes.todayPickup((state as? LoadState.Content)?.data?.totalMin)) },
        panel = {
            PanelHeader("오늘의 픽업", isMock = (state as? LoadState.Content)?.data?.isMock == true)
            when (state) {
                LoadState.Loading -> CenterLoading()
                is LoadState.Error -> CenterError(state.message, onRetry)
                is LoadState.Content -> Body(state.data, onChangeHome, onChoosePickupSpot)
            }
        },
    )
}

@Composable
private fun ColumnScope.Body(d: TodayPickupDetail, onChangeHome: () -> Unit, onChoosePickupSpot: () -> Unit) {
    val now = LocalDateTime.now(KstFormat.ZONE)
    val untilEnd = KstFormat.remaining(Duration.between(now, now.toLocalDate().atTime(d.item.endTime)))
    Column(
        Modifier
            .weight(1f)
            .padding(horizontal = 28.dp, vertical = 24.dp),
    ) {
        Label("오늘 하원 일정")
        Row(
            Modifier
                .fillMaxWidth()
                .clip(RoundedCornerShape(20.dp))
                .background(WayMeColors.Card)
                .padding(horizontal = 24.dp, vertical = 20.dp),
            verticalAlignment = Alignment.CenterVertically,
        ) {
            ChildAvatar(d.item.childName)
            Spacer(Modifier.width(20.dp))
            Column {
                Text("${d.item.childName} · ${d.item.academyName}", color = WayMeColors.TextPrimary, fontSize = 22.sp, fontWeight = FontWeight.Bold)
                Spacer(Modifier.height(4.dp))
                val parts = buildList {
                    add("${KstFormat.hm(d.item.endTime)} 하원")
                    d.areaName?.let { add(it) }
                    untilEnd?.let { add("하원까지 $it") }
                }
                Text(parts.joinToString("  ·  "), color = WayMeColors.TextSecondary, fontSize = 18.sp)
            }
        }

        Spacer(Modifier.height(24.dp))
        Label("집까지 경로")
        Column(
            Modifier
                .fillMaxWidth()
                .clip(RoundedCornerShape(20.dp))
                .background(WayMeColors.Card)
                .padding(24.dp),
        ) {
            RouteStop("현재 위치", sub = d.driveToPickupMin?.let { "차로 약 ${it}분" }, highlighted = false)
            RouteStop("${d.item.academyName} 근처", sub = d.pickupToHomeMin?.let { "차로 약 ${it}분" }, highlighted = true)
            Row(verticalAlignment = Alignment.CenterVertically) {
                Box(Modifier.weight(1f)) { RouteStop("집 · ${d.homeName}", sub = null, highlighted = false, last = true) }
                Button(
                    onClick = onChangeHome,
                    shape = RoundedCornerShape(12.dp),
                    colors = ButtonDefaults.buttonColors(containerColor = Color(0xFFE6E7EA), contentColor = Color(0xFF111111)),
                    modifier = Modifier.height(56.dp),
                ) { Text("장소 바꾸기", fontSize = 18.sp, fontWeight = FontWeight.Bold) }
            }
            Spacer(Modifier.height(16.dp))
            HorizontalDivider(color = WayMeColors.Divider)
            Spacer(Modifier.height(16.dp))
            Row(Modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically) {
                Text(d.totalMin?.let { "총 약 ${it}분" } ?: "총 소요 계산 중", color = WayMeColors.TextPrimary, fontSize = 22.sp, fontWeight = FontWeight.Bold)
                Spacer(Modifier.weight(1f))
                d.homeArrivalTime?.let { Text("집 도착 약 ${KstFormat.hm(it)}", color = WayMeColors.TextSecondary, fontSize = 18.sp) }
            }
        }
        Spacer(Modifier.weight(1f))
        PrimaryPanelButton("픽업 장소 고르기", onChoosePickupSpot)
    }
}

@Composable
internal fun RouteStop(title: String, sub: String?, highlighted: Boolean, last: Boolean = false) {
    Row {
        Column(horizontalAlignment = Alignment.CenterHorizontally, modifier = Modifier.width(24.dp)) {
            Canvas(Modifier.padding(top = 8.dp).size(14.dp)) {
                if (highlighted) drawCircle(WayMeColors.Accent, radius = size.minDimension / 2)
                else drawCircle(Color(0xFFDADCE0), radius = size.minDimension / 2 - 1.dp.toPx(), style = Stroke(2.dp.toPx()))
            }
            if (!last) Box(Modifier.width(2.dp).height(52.dp).background(WayMeColors.Divider))
        }
        Spacer(Modifier.width(16.dp))
        Column {
            Text(title, color = WayMeColors.TextPrimary, fontSize = 22.sp, fontWeight = if (highlighted) FontWeight.Bold else FontWeight.Normal)
            sub?.let { Text(it, color = WayMeColors.TextSecondary, fontSize = 17.sp) }
        }
    }
}

@Composable
internal fun Label(text: String) {
    Text(text, color = WayMeColors.TextSecondary, fontSize = 18.sp)
    Spacer(Modifier.height(12.dp))
}

@Composable
internal fun ColumnScope.CenterLoading() {
    Box(Modifier.weight(1f).fillMaxSize(), Alignment.Center) { CircularProgressIndicator(color = WayMeColors.Accent) }
}

@Composable
internal fun ColumnScope.CenterError(message: String, onRetry: () -> Unit) {
    Column(
        Modifier.weight(1f).fillMaxWidth().padding(28.dp),
        verticalArrangement = Arrangement.Center,
        horizontalAlignment = Alignment.CenterHorizontally,
    ) {
        Text(message, color = WayMeColors.TextPrimary, fontSize = 22.sp)
        Spacer(Modifier.height(20.dp))
        PrimaryPanelButton("다시 시도", onRetry)
    }
}
