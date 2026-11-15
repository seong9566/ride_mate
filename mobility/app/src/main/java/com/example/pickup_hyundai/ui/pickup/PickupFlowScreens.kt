package com.example.pickup_hyundai.ui.pickup

import com.example.pickup_hyundai.domain.voice.VoiceCommand
import com.example.pickup_hyundai.ui.components.VoiceCommandBar
import androidx.compose.foundation.Canvas
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.ColumnScope
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.shape.RoundedCornerShape
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
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.example.pickup_hyundai.core.time.KstFormat
import com.example.pickup_hyundai.domain.model.WaitingPlace
import com.example.pickup_hyundai.map.MapPanel
import com.example.pickup_hyundai.map.MapScenes
import com.example.pickup_hyundai.ui.components.DevAdvanceChip
import com.example.pickup_hyundai.ui.components.PanelHeader
import com.example.pickup_hyundai.ui.components.PrimaryPanelButton
import com.example.pickup_hyundai.ui.components.WayMeScaffold
import com.example.pickup_hyundai.ui.theme.WayMeColors
import kotlinx.coroutines.delay
import java.time.Duration
import java.time.LocalDateTime
import java.time.LocalTime

/** V07A 대기 중 화면 데이터. 대기 장소·주차 요금·집까지 소요는 정본 API가 없어 Mock(isMock) */
data class WaitingStayInfo(
    val place: WaitingPlace?,
    val placeName: String,
    /** 출발해야 하는 시각 = 픽업 도착 목표 − 차량 소요 */
    val departAt: LocalDateTime?,
    val endTime: LocalTime?,
    /** 하원보다 몇 분 먼저 도착하는지(디자인 "5분 전 도착") */
    val arriveBeforeMin: Int?,
    val driveToPickupMin: Int?,
    val pickupName: String,
    val pickupArrive: LocalTime?,
    val toHomeMin: Int?,
    val homeArrive: LocalTime?,
    val feeText: String?,
    val isMock: Boolean,
)

@Composable
private fun rememberNow(periodMs: Long = 15_000): LocalDateTime {
    var now by remember { mutableStateOf(LocalDateTime.now(KstFormat.ZONE)) }
    LaunchedEffect(periodMs) { while (true) { delay(periodMs); now = LocalDateTime.now(KstFormat.ZONE) } }
    return now
}

/** V07A 대기 중 — 디자인: 대기장소 도착 후 "출발까지 남은 시간" */
@Composable
fun WaitingStayScreen(info: WaitingStayInfo, onDepartNow: () -> Unit) {
    WayMeScaffold(
        map = {
            MapPanel(MapScenes.waitingStay(info.place, info.driveToPickupMin, info.pickupName))
        },
        panel = {
            PanelHeader("대기 중", isMock = info.isMock)
            WaitingStayBody(info, onDepartNow)
        },
    )
}

@Composable
private fun ColumnScope.WaitingStayBody(info: WaitingStayInfo, onDepartNow: () -> Unit) {
    val now = rememberNow()
    Column(Modifier.weight(1f).padding(horizontal = 28.dp, vertical = 24.dp)) {
        Text("출발까지 남은 시간", color = WayMeColors.TextSecondary, fontSize = 20.sp)
        val left = info.departAt?.let { Duration.between(now, it).toMinutes() }
        Text(
            when {
                left == null -> "--"
                left <= 0 -> "지금"
                else -> "${left}분"
            },
            color = if (left != null && left <= 0) WayMeColors.Accent else WayMeColors.TextPrimary,
            fontSize = 80.sp, fontWeight = FontWeight.Bold,
        )
        Text(
            listOfNotNull(
                info.departAt?.let { "${KstFormat.hm(it.toLocalTime())} 출발" },
                info.endTime?.let { "${KstFormat.hm(it)} 하원" },
                info.arriveBeforeMin?.takeIf { it > 0 }?.let { "${it}분 전 도착" },
            ).joinToString(" · "),
            color = WayMeColors.TextPrimary, fontSize = 22.sp,
        )
        Spacer(Modifier.height(28.dp))
        Column(
            Modifier.fillMaxWidth().clip(RoundedCornerShape(20.dp)).background(WayMeColors.Card).padding(24.dp),
        ) {
            RouteStop(info.placeName, sub = listOfNotNull("지금", info.driveToPickupMin?.let { "차로 ${it}분" }).joinToString(" · "), highlighted = true)
            RouteStop(
                info.pickupName,
                sub = listOfNotNull(info.pickupArrive?.let { "${KstFormat.hm(it)} 도착" }, info.toHomeMin?.let { "차로 약 ${it}분" }).joinToString(" · ").ifEmpty { null },
                highlighted = false,
            )
            RouteStop("집", sub = info.homeArrive?.let { "${KstFormat.hm(it)} 도착" }, highlighted = false, last = true)
        }
        info.feeText?.let {
            Spacer(Modifier.height(16.dp))
            Text("주차 요금 $it", color = WayMeColors.TextSecondary, fontSize = 18.sp)
        }
        Spacer(Modifier.weight(1f))
        VoiceCommandBar(screen = "WAITING_STAY", idleText = "\"출발\"이라고 말하면 바로 안내해요") { cmd ->
            if (cmd == VoiceCommand.DEPART || cmd == VoiceCommand.YES) { onDepartNow(); true } else false
        }
        Spacer(Modifier.height(16.dp))
        PrimaryPanelButton("지금 출발", onDepartNow)
    }
}

/** V08 픽업 장소 안내 화면 데이터 */
data class PickupGuideInfo(
    /** 배정 도착 시각(정본 AddPickupPlan.slotDt) */
    val assignedArrival: LocalTime?,
    val endTime: LocalTime?,
    val pickupName: String,
    val walkMin: Int?,
    /** 정본 enforcement.status == RESTRICTED */
    val restricted: Boolean,
    val guideText: String?,
    val remainMin: Int?,
    val distanceKm: Double?,
    val isMock: Boolean,
)

/** V08 픽업 장소 안내 — 디자인: "배정된 도착 시각 22:04", 단속 구역 안내 카드, 음성 안내 상태 */
@Composable
fun PickupGuideScreen(info: PickupGuideInfo, naviGuiding: Boolean, onDevAdvance: () -> Unit) {
    val chip = listOfNotNull(
        info.assignedArrival?.let { KstFormat.hm(it) },
        info.distanceKm?.let { "%.1fkm".format(it) },
        info.remainMin?.let { "${it}분" },
    ).joinToString(" · ").ifEmpty { null }
    WayMeScaffold(
        map = {
            MapPanel(MapScenes.pickupGuide(info.pickupName, info.walkMin, info.restricted, chip), followVehicle = true)
            DevAdvanceChip("픽업 장소 도착", onDevAdvance)
        },
        panel = {
            PanelHeader("픽업 장소 안내", isMock = info.isMock)
            PickupGuideBody(info, naviGuiding)
        },
    )
}

@Composable
private fun ColumnScope.PickupGuideBody(info: PickupGuideInfo, naviGuiding: Boolean) {
    Column(Modifier.weight(1f).padding(horizontal = 28.dp, vertical = 24.dp)) {
        Text("배정된 도착 시각", color = WayMeColors.TextSecondary, fontSize = 20.sp)
        Text(info.assignedArrival?.let(KstFormat::hm) ?: "--:--", color = WayMeColors.TextPrimary, fontSize = 80.sp, fontWeight = FontWeight.Bold)
        Spacer(Modifier.height(12.dp))
        Row(verticalAlignment = Alignment.CenterVertically) {
            PinIcon(Modifier.size(width = 20.dp, height = 26.dp))
            Spacer(Modifier.width(12.dp))
            Text(info.pickupName, color = WayMeColors.TextPrimary, fontSize = 24.sp, fontWeight = FontWeight.Bold)
            info.walkMin?.let {
                Spacer(Modifier.width(12.dp))
                Text(
                    "아이 도보 ${it}분", color = WayMeColors.Accent, fontSize = 16.sp, fontWeight = FontWeight.Bold,
                    modifier = Modifier.clip(RoundedCornerShape(50)).background(Color(0xFF3A4220)).padding(horizontal = 12.dp, vertical = 4.dp),
                )
            }
        }
        Spacer(Modifier.height(24.dp))
        if (info.restricted) {
            val shape = RoundedCornerShape(20.dp)
            Row(
                Modifier.fillMaxWidth().clip(shape).background(Color(0xFF3A2020)).border(1.dp, Color(0xFF5A2E2E), shape)
                    .padding(horizontal = 24.dp, vertical = 20.dp),
                verticalAlignment = Alignment.CenterVertically,
            ) {
                WarningTriangle(Modifier.size(26.dp))
                Spacer(Modifier.width(16.dp))
                Column {
                    Text("학원 앞은 단속 구역이에요", color = WayMeColors.TextPrimary, fontSize = 22.sp, fontWeight = FontWeight.Bold)
                    Spacer(Modifier.height(4.dp))
                    Text(restrictedDetail(info), color = WayMeColors.TextSecondary, fontSize = 17.sp)
                }
            }
        } else {
            StatusCard("배정된 픽업 장소로 안내해요", info.guideText, warning = false) { WalkerIcon(Modifier.size(24.dp)) }
        }
        Spacer(Modifier.weight(1f))
        // 이동 중 화면 하단 음성 아이콘: 탭하면 차량 마이크로 듣기(길안내 음성 자체는 Pleos 기본 내비가 담당)
        VoiceCommandBar(
            screen = "PICKUP_GUIDE",
            idleText = if (naviGuiding) "음성으로 안내하고 있어요" else "내비 경로를 준비하고 있어요",
        )
    }
}

/** "걸어서 3분 거리 ○○ 앞으로 안내해요 · 10시엔 차가 몰려 10시 4분으로 맞췄어요" */
private fun restrictedDetail(info: PickupGuideInfo): String = buildString {
    append(info.walkMin?.let { "걸어서 ${it}분 거리 " } ?: "")
    append("${info.pickupName}(으)로 안내해요")
    val end = info.endTime
    val slot = info.assignedArrival
    if (end != null && slot != null && slot.isAfter(end)) append(" · ${spoken(end)}엔 차가 몰려 ${spoken(slot)}으로 맞췄어요")
}

private fun spoken(t: LocalTime): String {
    val h = (t.hour % 12).let { if (it == 0) 12 else it }
    return if (t.minute == 0) "${h}시" else "${h}시 ${t.minute}분"
}

@Composable
private fun PinIcon(modifier: Modifier) {
    Canvas(modifier) {
        drawCircle(WayMeColors.Accent, radius = size.width / 2, center = Offset(size.width / 2, size.width / 2))
        drawLine(WayMeColors.Accent, Offset(size.width / 2, size.width), Offset(size.width / 2, size.height), strokeWidth = 3.dp.toPx())
        drawCircle(WayMeColors.OnAccent, radius = size.width / 5, center = Offset(size.width / 2, size.width / 2))
    }
}

@Composable
private fun VoiceBars(active: Boolean) {
    val c = if (active) WayMeColors.Accent else WayMeColors.TextTertiary
    Canvas(Modifier.size(width = 28.dp, height = 24.dp)) {
        val hs = floatArrayOf(0.4f, 0.8f, 1f, 0.6f)
        val w = size.width / (hs.size * 2 - 1)
        hs.forEachIndexed { i, h ->
            val x = i * 2 * w + w / 2
            drawLine(c, Offset(x, size.height * (1 - h) / 2), Offset(x, size.height * (1 + h) / 2), strokeWidth = w)
        }
    }
}
