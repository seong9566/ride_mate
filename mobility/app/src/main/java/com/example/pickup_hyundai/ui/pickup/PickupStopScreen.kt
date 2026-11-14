package com.example.pickup_hyundai.ui.pickup

import com.example.pickup_hyundai.map.MapScenes
import com.example.pickup_hyundai.map.MapPanel
import androidx.compose.foundation.Canvas
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
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.StrokeCap
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.example.pickup_hyundai.core.time.KstFormat
import com.example.pickup_hyundai.domain.StopRules
import com.example.pickup_hyundai.domain.StopRules.ChildStatus
import com.example.pickup_hyundai.domain.StopRules.TimerPhase
import com.example.pickup_hyundai.domain.model.PickupStopInfo
import com.example.pickup_hyundai.ui.components.MapPlaceholder
import com.example.pickup_hyundai.ui.components.DevAdvanceChip
import com.example.pickup_hyundai.ui.components.PanelHeader
import com.example.pickup_hyundai.ui.components.PrimaryPanelButton
import com.example.pickup_hyundai.ui.components.WayMeScaffold
import com.example.pickup_hyundai.ui.theme.WayMeColors
import kotlinx.coroutines.delay
import java.time.Duration
import java.time.LocalDateTime

private val TimerOrange = Color(0xFFF5A623)
private val TimerRed = Color(0xFFE5483B)

/**
 * V09 픽업 정차 — 디자인: 4.메세지보낸 다음_(3)지금나가요_잔여초 30초이상 / _30초이내 / _아이미확인 / _정차시간초과
 * 정차 타이머 경계는 StopRules(§4-5)를 따른다.
 */
@Composable
fun PickupStopScreen(
    state: PickupStopUiState,
    onRemind: () -> Unit,
    onRetry: () -> Unit,
    /** 학원 → 픽업 장소 아이 도보(분). 정본 pickup.walkMin */
    walkMin: Int? = null,
    /** 뒷문 열림→닫힘(탑승 후보). 문 신호만으로 확정하지 않고 운전자 확인을 받는다(08_USER_FLOW §4-7) */
    boardingCandidate: Boolean = false,
    onConfirmBoarded: () -> Unit = {},
    onDevBoarding: () -> Unit = {},
) {
    val load = state.load
    val info = (load as? LoadState.Content)?.data
    WayMeScaffold(
        map = {
            MapPanel(MapScenes.pickupStop())
            if (info?.inRestrictedZone == true) {
                RestrictedZoneBanner(info.restrictedMessage, Modifier.padding(20.dp))
            }
            DevAdvanceChip("뒷문 열림→닫힘", onDevBoarding)
        },
        panel = {
            PanelHeader("픽업 정차", isMock = info?.isMock == true)
            when (load) {
                LoadState.Loading -> CenterLoading()
                is LoadState.Error -> CenterError(load.message, onRetry)
                is LoadState.Content -> Body(load.data, state, onRemind, walkMin, boardingCandidate, onConfirmBoarded)
            }
        },
    )
}

@Composable
private fun ColumnScope.Body(
    info: PickupStopInfo,
    state: PickupStopUiState,
    onRemind: () -> Unit,
    walkMin: Int?,
    boardingCandidate: Boolean,
    onConfirmBoarded: () -> Unit,
) {
    var now by remember { mutableStateOf(LocalDateTime.now(KstFormat.ZONE)) }
    LaunchedEffect(Unit) { while (true) { delay(1_000); now = LocalDateTime.now(KstFormat.ZONE) } }

    Column(
        Modifier
            .weight(1f)
            .padding(horizontal = 28.dp, vertical = 24.dp),
    ) {
        val phase = info.allowedStopSec?.let { allowed ->
            val remain = StopRules.remainingSec(now, info.stopStartedAt, allowed)
            StopTimer(remain, allowed)
            Spacer(Modifier.height(28.dp))
            StopRules.phase(remain)
        }
        val walkText = walkMin?.let { "학원에서 걸어서 약 ${it}분 걸려요" }

        when (StopRules.childStatus(info.delivery)) {
            ChildStatus.LEAVING -> StatusCard(
                "아이가 나오고 있어요",
                walkText ?: info.delivery.leavingAt?.let { "'지금 나가요' ${KstFormat.hm(it.toLocalTime())}" },
                warning = false,
                highlighted = true,
            ) { WalkerIcon(Modifier.size(24.dp)) }
            ChildStatus.OPENED -> StatusCard(
                "아이가 링크를 확인했어요",
                info.delivery.openedAt?.let { "링크 확인 ${KstFormat.hm(it.toLocalTime())}" },
                warning = false,
            ) { WalkerIcon(Modifier.size(24.dp)) }
            ChildStatus.NOT_OPENED -> StatusCard(
                "아이가 아직 확인하지 않았어요",
                info.delivery.sentAt?.let { "픽업 장소를 보냈어요  ·  ${Duration.between(it, now).toMinutes()}분 전" },
                warning = true,
            ) { WarningTriangle(Modifier.size(24.dp)) }
            ChildStatus.NOT_SENT -> StatusCard("아직 픽업 장소를 보내지 않았어요", null, warning = true) { WarningTriangle(Modifier.size(24.dp)) }
        }

        Spacer(Modifier.height(24.dp))
        DeliveryStepper(info.delivery)
        Spacer(Modifier.height(16.dp))
        Row(verticalAlignment = Alignment.CenterVertically) {
            DoorIcon(Modifier.size(width = 16.dp, height = 20.dp))
            Spacer(Modifier.width(12.dp))
            // 디자인 문구. 실제 동작은 §4-7에 따라 운전자 확인 후 집 경로 안내(5단계에서 구현)
            Text("뒷문이 열렸다 닫히면 자동으로 집 경로를 안내해요", color = WayMeColors.TextSecondary, fontSize = 17.sp)
        }
        state.message?.let {
            Spacer(Modifier.height(12.dp))
            Text(it, color = WayMeColors.Accent, fontSize = 18.sp)
        }
        Spacer(Modifier.weight(1f))
        if (boardingCandidate) {
            // 뒷문 열림→닫힘 감지 → 운전자 확인 후 집 경로 요청
            PrimaryPanelButton("아이가 탔어요 · 집으로 출발", onConfirmBoarded)
        } else {
            // 디자인: 남은 정차 시간이 넉넉할 때(30초 초과)는 다시 알리기 비활성, 30초 이내·초과 시 활성
            val remindAllowed = phase == null || phase != TimerPhase.NORMAL
            PrimaryPanelButton(
                if (state.reminding) "알리는 중…" else "아이에게 \"도착했어요\" 다시 알리기",
                onRemind,
                enabled = !state.reminding && remindAllowed,
            )
        }
    }
}

@Composable
private fun StopTimer(remainSec: Long, allowedSec: Int) {
    val phase = StopRules.phase(remainSec)
    val color = when (phase) {
        TimerPhase.NORMAL -> WayMeColors.Accent
        TimerPhase.WARNING -> TimerOrange
        TimerPhase.OVER -> TimerRed
    }
    Row(verticalAlignment = Alignment.CenterVertically) {
        Box(Modifier.size(160.dp), contentAlignment = Alignment.Center) {
            Canvas(Modifier.size(160.dp)) {
                val w = 8.dp.toPx()
                val inset = w / 2
                val arcSize = androidx.compose.ui.geometry.Size(size.width - w, size.height - w)
                val topLeft = androidx.compose.ui.geometry.Offset(inset, inset)
                drawArc(Color(0xFF34363B), 0f, 360f, false, topLeft, arcSize, style = Stroke(w))
                val sweep = if (phase == TimerPhase.OVER) 360f else 360f * remainSec / allowedSec
                drawArc(color.copy(alpha = if (phase == TimerPhase.OVER) 0.45f else 1f), -90f, sweep, false, topLeft, arcSize, style = Stroke(w, cap = StrokeCap.Round))
            }
            Column(horizontalAlignment = Alignment.CenterHorizontally) {
                Text("%d:%02d".format(remainSec / 60, remainSec % 60), color = WayMeColors.TextPrimary, fontSize = 44.sp, fontWeight = FontWeight.Bold)
                Text("남은 시간", color = WayMeColors.TextSecondary, fontSize = 14.sp)
            }
        }
        Spacer(Modifier.width(28.dp))
        Column {
            Text(
                if (phase == TimerPhase.OVER) "정차 시간 초과" else "남은 정차 시간",
                color = color, fontSize = 18.sp,
            )
            Spacer(Modifier.height(4.dp))
            Text(
                when (phase) {
                    TimerPhase.NORMAL -> "${(remainSec + 59) / 60}분 안에 출발해요"
                    TimerPhase.WARNING -> "곧 출발해야 해요"
                    TimerPhase.OVER -> "정차 시간이 지났어요"
                },
                color = WayMeColors.TextPrimary, fontSize = 28.sp, fontWeight = FontWeight.Bold,
            )
        }
    }
}
