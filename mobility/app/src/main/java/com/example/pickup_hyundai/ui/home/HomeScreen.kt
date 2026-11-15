package com.example.pickup_hyundai.ui.home

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
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.geometry.CornerRadius
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.text.SpanStyle
import androidx.compose.ui.text.buildAnnotatedString
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.withStyle
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.example.pickup_hyundai.core.time.KstFormat
import com.example.pickup_hyundai.domain.model.HomeSummary
import com.example.pickup_hyundai.domain.model.PickupItem
import com.example.pickup_hyundai.domain.model.WeekDaySchedule
import com.example.pickup_hyundai.ui.components.MapPlaceholder
import com.example.pickup_hyundai.ui.components.PanelHeader
import com.example.pickup_hyundai.ui.components.WayMeScaffold
import com.example.pickup_hyundai.ui.theme.WayMeColors
import kotlinx.coroutines.delay
import java.time.Duration
import java.time.LocalDate
import java.time.LocalDateTime

/** V02 홈 — 디자인: 홈화면_픽업일정확인/오늘의 픽업 일정(.png), 오늘의 픽업 일정이 없는 경우(.png) */
@Composable
fun HomeScreen(
    state: HomeUiState,
    onOpenTodayPickup: () -> Unit,
    onOpenTimetable: () -> Unit,
    onRetry: () -> Unit,
    /** V01 오늘의 픽업 알림(앱을 켰을 때 지도 위 카드). 보기 → 오늘 하원 일정, 닫기 → 숨김 */
    showAlert: Boolean = false,
    onDismissAlert: () -> Unit = {},
) {
    val isMock = (state as? HomeUiState.Content)?.summary?.isMock == true
    WayMeScaffold(
        map = {
            val summary = (state as? HomeUiState.Content)?.summary
            val first = summary?.todayItems?.firstOrNull()
            val alert = showAlert && first != null
            val scene = if (first != null) MapScenes.overview(pickupName = first.pickupPlaceName ?: "픽업 장소", academyName = first.academyName)
            else MapScenes.overview()
            // 알림 카드가 떠 있는 동안에는 같은 자리의 요약 칩을 숨긴다
            MapPanel(if (alert) scene.copy(info = null) else scene)
            if (alert && first != null) {
                PickupAlertCard(
                    body = "${first.academyName} ${KstFormat.hm(first.endTime)} 하원" +
                        (summary?.nextDepartureDt?.let { " · ${KstFormat.hm(it.toLocalTime())}에 출발하세요" } ?: ""),
                    onOpen = { onDismissAlert(); onOpenTodayPickup() },
                    onClose = onDismissAlert,
                )
            }
        },
        panel = {
            PanelHeader(screenName = "홈", isMock = isMock)
            when (state) {
                HomeUiState.Loading -> Box(Modifier.fillMaxSize(), Alignment.Center) {
                    CircularProgressIndicator(color = WayMeColors.Accent)
                }
                is HomeUiState.Error -> ErrorBody(state.message, onRetry)
                is HomeUiState.Content ->
                    if (state.summary.todayItems.isNotEmpty()) TodayBody(state.summary, onOpenTodayPickup)
                    else EmptyBody(state.summary, onOpenTimetable)
            }
        },
    )
}

@Composable
private fun rememberNowKst(): LocalDateTime {
    var now by remember { mutableStateOf(LocalDateTime.now(KstFormat.ZONE)) }
    LaunchedEffect(Unit) {
        while (true) {
            delay(30_000)
            now = LocalDateTime.now(KstFormat.ZONE)
        }
    }
    return now
}

@Composable
private fun ColumnScope.TodayBody(summary: HomeSummary, onOpenTodayPickup: () -> Unit) {
    val now = rememberNowKst()
    Column(
        Modifier
            .weight(1f)
            .padding(horizontal = 28.dp, vertical = 24.dp),
    ) {
        // 디자인(오늘의 픽업 일정.png): 큰 시각 = 첫 하원 시각, 아래 "하원까지 N시간 N분"
        Text("하원 시간", color = WayMeColors.TextSecondary, fontSize = 20.sp)
        val end = summary.firstEndTime
        Text(
            end?.let(KstFormat::hm) ?: "--:--",
            color = WayMeColors.TextPrimary,
            fontSize = 80.sp,
            fontWeight = FontWeight.Bold,
        )
        val remain = end?.let { KstFormat.remaining(Duration.between(now, now.toLocalDate().atTime(it))) }
        Text(
            buildAnnotatedString {
                when {
                    end == null -> append("하원 시각 확인 중")
                    remain == null -> withStyle(SpanStyle(color = WayMeColors.Warning)) { append("하원 시각이 지났어요") }
                    else -> {
                        append("하원까지 ")
                        withStyle(SpanStyle(color = WayMeColors.Accent, fontWeight = FontWeight.Bold)) { append(remain) }
                    }
                }
            },
            color = WayMeColors.TextPrimary,
            fontSize = 22.sp,
        )
        Spacer(Modifier.height(28.dp))
        Text("오늘 일정 · ${summary.todayItems.size}건", color = WayMeColors.TextSecondary, fontSize = 18.sp)
        Spacer(Modifier.height(12.dp))
        Column(verticalArrangement = Arrangement.spacedBy(12.dp)) {
            // 주행 중 긴 스크롤 금지 정책 → 최대 2건만 노출
            summary.todayItems.take(2).forEach { item ->
                PickupCard(item, subtitle = "${KstFormat.hm(item.endTime)} 하원")
            }
        }
        Spacer(Modifier.height(28.dp))
        WeekRow(summary.week, today = now.toLocalDate())
        Spacer(Modifier.weight(1f))
        PrimaryButton("오늘의 픽업 시작", onOpenTodayPickup)
        Spacer(Modifier.height(16.dp))
        Footnote("출발 30분 전이 되면 오늘의 픽업 화면이 바로 열려요")
    }
}

@Composable
private fun ColumnScope.EmptyBody(summary: HomeSummary, onOpenTimetable: () -> Unit) {
    val now = rememberNowKst()
    Column(
        Modifier
            .weight(1f)
            .padding(horizontal = 28.dp, vertical = 24.dp),
    ) {
        CalendarIcon()
        Spacer(Modifier.height(24.dp))
        Text("오늘은 픽업 일정이 없어요", color = WayMeColors.TextPrimary, fontSize = 28.sp, fontWeight = FontWeight.Bold)
        Spacer(Modifier.height(8.dp))
        Text("시간표에 등록된 요일에만 출발 시각을 알려 드려요", color = WayMeColors.TextSecondary, fontSize = 20.sp)
        summary.nextPickup?.let { next ->
            Spacer(Modifier.height(28.dp))
            Text("다음 픽업", color = WayMeColors.TextSecondary, fontSize = 18.sp)
            Spacer(Modifier.height(12.dp))
            PickupCard(
                next.item,
                subtitle = "${KstFormat.relativeDay(next.date, now.toLocalDate())} ${KstFormat.hm(next.item.endTime)} 하원" +
                    (next.item.pickupPlaceName?.let { "  ·  $it" } ?: ""),
            )
        }
        Spacer(Modifier.height(28.dp))
        WeekRow(summary.week, today = now.toLocalDate())
        Spacer(Modifier.weight(1f))
        PrimaryButton("시간표 보기", onOpenTimetable)
        Spacer(Modifier.height(16.dp))
        Footnote("시간표는 정차 중에만 바꿀 수 있어요")
    }
}

@Composable
private fun ColumnScope.ErrorBody(message: String, onRetry: () -> Unit) {
    Column(
        Modifier
            .weight(1f)
            .fillMaxWidth()
            .padding(28.dp),
        verticalArrangement = Arrangement.Center,
        horizontalAlignment = Alignment.CenterHorizontally,
    ) {
        Text(message, color = WayMeColors.TextPrimary, fontSize = 22.sp)
        Spacer(Modifier.height(20.dp))
        PrimaryButton("다시 시도", onRetry)
    }
}

@Composable
private fun PickupCard(item: PickupItem, subtitle: String) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(20.dp))
            .background(WayMeColors.Card)
            .padding(horizontal = 24.dp, vertical = 20.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Box(
            Modifier
                .size(52.dp)
                .clip(CircleShape)
                .background(WayMeColors.Avatar),
            contentAlignment = Alignment.Center,
        ) {
            Text(item.childName.take(1), color = WayMeColors.TextPrimary, fontSize = 20.sp, fontWeight = FontWeight.Bold)
        }
        Spacer(Modifier.width(20.dp))
        Column {
            Text("${item.childName} · ${item.academyName}", color = WayMeColors.TextPrimary, fontSize = 22.sp, fontWeight = FontWeight.Bold)
            Spacer(Modifier.height(4.dp))
            Text(subtitle, color = WayMeColors.TextSecondary, fontSize = 18.sp)
        }
    }
}

@Composable
private fun WeekRow(week: List<WeekDaySchedule>, today: LocalDate) {
    Text("이번 주", color = WayMeColors.TextSecondary, fontSize = 18.sp)
    Spacer(Modifier.height(12.dp))
    Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
        week.forEach { day ->
            val isToday = day.date == today
            Column(
                modifier = Modifier
                    .weight(1f)
                    .clip(RoundedCornerShape(14.dp))
                    .background(if (isToday) WayMeColors.CardSelected else WayMeColors.Card)
                    .padding(vertical = 14.dp),
                horizontalAlignment = Alignment.CenterHorizontally,
            ) {
                Text(
                    if (isToday) "오늘" else KstFormat.dayShort(day.dayOfWeek),
                    color = if (isToday) WayMeColors.Accent else WayMeColors.TextPrimary,
                    fontSize = 18.sp,
                    fontWeight = FontWeight.Bold,
                )
                Spacer(Modifier.height(4.dp))
                Text(
                    buildAnnotatedString {
                        append(day.firstEndTime?.let(KstFormat::hm) ?: "–")
                        if (day.extraCount > 0) withStyle(SpanStyle(color = WayMeColors.TextTertiary, fontSize = 13.sp)) { append(" +${day.extraCount}") }
                    },
                    color = if (day.firstEndTime == null) WayMeColors.TextTertiary else WayMeColors.TextPrimary,
                    fontSize = 17.sp,
                    fontWeight = FontWeight.Bold,
                )
            }
        }
    }
}

@Composable
private fun PrimaryButton(text: String, onClick: () -> Unit) {
    Button(
        onClick = onClick,
        modifier = Modifier
            .fillMaxWidth()
            .height(76.dp),
        shape = RoundedCornerShape(20.dp),
        colors = ButtonDefaults.buttonColors(containerColor = WayMeColors.Card, contentColor = WayMeColors.TextPrimary),
    ) {
        Text(text, fontSize = 22.sp, fontWeight = FontWeight.Bold)
    }
}

@Composable
private fun ColumnScope.Footnote(text: String) {
    Text(text, color = WayMeColors.TextSecondary, fontSize = 16.sp, modifier = Modifier.align(Alignment.CenterHorizontally))
}

@Composable
private fun CalendarIcon() {
    Box(
        Modifier
            .size(64.dp)
            .clip(RoundedCornerShape(16.dp))
            .background(WayMeColors.Card),
        contentAlignment = Alignment.Center,
    ) {
        Canvas(Modifier.size(28.dp)) {
            val stroke = Stroke(width = 2.dp.toPx())
            val c = Color(0xFFDADCE0)
            drawRoundRect(c, topLeft = Offset(0f, size.height * 0.12f), size = androidx.compose.ui.geometry.Size(size.width, size.height * 0.88f), cornerRadius = CornerRadius(4.dp.toPx()), style = stroke)
            drawLine(c, Offset(0f, size.height * 0.4f), Offset(size.width, size.height * 0.4f), strokeWidth = stroke.width)
            drawLine(c, Offset(size.width * 0.3f, 0f), Offset(size.width * 0.3f, size.height * 0.25f), strokeWidth = stroke.width)
            drawLine(c, Offset(size.width * 0.7f, 0f), Offset(size.width * 0.7f, size.height * 0.25f), strokeWidth = stroke.width)
        }
    }
}

/** V01 알림 카드 — 디자인: 로고 · "오늘의 픽업 일정이 도착했어요" · "수학학원 22:00 하원 · 21:29에 출발하세요" · 보기/닫기 */
@Composable
private fun androidx.compose.foundation.layout.BoxScope.PickupAlertCard(body: String, onOpen: () -> Unit, onClose: () -> Unit) {
    val shape = RoundedCornerShape(24.dp)
    Column(
        Modifier
            .align(Alignment.TopStart)
            .padding(start = 20.dp, top = 20.dp, end = 104.dp)
            .fillMaxWidth()
            .clip(shape)
            .background(Color(0xF21E1F22))
            .padding(20.dp),
    ) {
        Row(verticalAlignment = Alignment.CenterVertically) {
            androidx.compose.foundation.Image(
                painter = androidx.compose.ui.res.painterResource(com.example.pickup_hyundai.R.drawable.img_wayme_logo),
                contentDescription = null,
                modifier = Modifier.size(56.dp).clip(RoundedCornerShape(12.dp)),
            )
            Spacer(Modifier.width(16.dp))
            Column {
                Text("오늘의 픽업 일정이 도착했어요", color = WayMeColors.TextPrimary, fontSize = 22.sp, fontWeight = FontWeight.Bold)
                Spacer(Modifier.height(4.dp))
                Text(body, color = WayMeColors.TextSecondary, fontSize = 18.sp)
            }
        }
        Spacer(Modifier.height(16.dp))
        Row(horizontalArrangement = Arrangement.spacedBy(12.dp)) {
            Button(
                onClick = onClose,
                modifier = Modifier.weight(1f).height(64.dp),
                shape = RoundedCornerShape(16.dp),
                colors = ButtonDefaults.buttonColors(containerColor = WayMeColors.Card, contentColor = WayMeColors.TextPrimary),
            ) { Text("닫기", fontSize = 20.sp, fontWeight = FontWeight.Bold) }
            Button(
                onClick = onOpen,
                modifier = Modifier.weight(1f).height(64.dp),
                shape = RoundedCornerShape(16.dp),
                colors = ButtonDefaults.buttonColors(containerColor = WayMeColors.Accent, contentColor = WayMeColors.OnAccent),
            ) { Text("보기", fontSize = 20.sp, fontWeight = FontWeight.Bold) }
        }
    }
}
