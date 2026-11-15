package com.example.pickup_hyundai.ui.pickup

import androidx.compose.foundation.Canvas
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.layout.Box
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
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.Path
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.example.pickup_hyundai.core.time.KstFormat
import com.example.pickup_hyundai.domain.model.DeliveryStep
import com.example.pickup_hyundai.domain.model.LinkDelivery
import com.example.pickup_hyundai.ui.theme.WayMeColors
import com.example.pickup_hyundai.ui.theme.WayMeExtraColors

private val DoneStepBg = Color(0xFF3A4220)

/** 디자인 "전달 상태" 3단계 표시 */
@Composable
fun DeliveryStepper(delivery: LinkDelivery) {
    Text("전달 상태", color = WayMeColors.TextSecondary, fontSize = 18.sp)
    Spacer(Modifier.height(12.dp))
    val steps = listOf(DeliveryStep.SENT to "보냄", DeliveryStep.OPENED to "열람", DeliveryStep.LEAVING to "지금 나가요")
    Row(Modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically) {
        steps.forEachIndexed { i, (step, label) ->
            val done = delivery.isDone(step)
            val isLatest = done && delivery.reached == step
            // 디자인: 완료된 단계는 시각을 함께 표시("보냄 · 21:58")
            val at = when (step) {
                DeliveryStep.SENT -> delivery.sentAt
                DeliveryStep.OPENED -> delivery.openedAt
                DeliveryStep.LEAVING -> delivery.leavingAt
            }
            StepChip(at?.let { "$label · ${KstFormat.hm(it.toLocalTime())}" } ?: label, done = done, latest = isLatest)
            if (i < steps.lastIndex) {
                val nextDone = delivery.isDone(steps[i + 1].first)
                Box(
                    Modifier
                        .weight(1f)
                        .padding(horizontal = 8.dp)
                        .height(2.dp)
                        .background(if (nextDone) WayMeColors.Accent else WayMeColors.Divider),
                )
            }
        }
    }
}

@Composable
private fun StepChip(label: String, done: Boolean, latest: Boolean) {
    val shape = RoundedCornerShape(50)
    val (bg, fg) = when {
        latest -> WayMeColors.Accent to WayMeColors.OnAccent
        done -> DoneStepBg to WayMeColors.TextPrimary
        else -> Color.Transparent to WayMeColors.TextSecondary
    }
    Row(
        Modifier
            .clip(shape)
            .background(bg)
            .then(if (!done) Modifier.border(1.5.dp, WayMeColors.Divider, shape) else Modifier)
            .padding(horizontal = 16.dp, vertical = 8.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Text(if (done) "✓ " else "•  ", color = fg, fontSize = 16.sp, fontWeight = FontWeight.Bold)
        Text(label, color = fg, fontSize = 18.sp, fontWeight = if (done) FontWeight.Bold else FontWeight.Normal)
    }
}

/** 지도 패널 상단 단속 구역 경고 배너(디자인 "단속 구역이에요") */
@Composable
fun RestrictedZoneBanner(message: String?, modifier: Modifier = Modifier) {
    Row(
        modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(18.dp))
            .background(Color(0xFF2E1B1B))
            .border(1.dp, Color(0xFF4A2A2A), RoundedCornerShape(18.dp))
            .padding(horizontal = 20.dp, vertical = 14.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        WarningTriangle(Modifier.size(26.dp))
        Spacer(Modifier.width(16.dp))
        androidx.compose.foundation.layout.Column(Modifier.weight(1f)) {
            Text("단속 구역이에요", color = WayMeColors.TextPrimary, fontSize = 20.sp, fontWeight = FontWeight.Bold)
            message?.let { Text(it, color = WayMeColors.TextSecondary, fontSize = 16.sp) }
        }
    }
}

@Composable
fun WarningTriangle(modifier: Modifier = Modifier, color: Color = WayMeColors.Warning) {
    Canvas(modifier) {
        val p = Path().apply {
            moveTo(size.width / 2, 0f); lineTo(size.width, size.height); lineTo(0f, size.height); close()
        }
        drawPath(p, color, style = Stroke(2.dp.toPx()))
        drawLine(color, Offset(size.width / 2, size.height * 0.35f), Offset(size.width / 2, size.height * 0.68f), strokeWidth = 2.5.dp.toPx())
        drawCircle(color, radius = 1.6.dp.toPx(), center = Offset(size.width / 2, size.height * 0.83f))
    }
}

/** 걷는 사람 아이콘(간단 도형) */
@Composable
fun WalkerIcon(modifier: Modifier = Modifier, color: Color = WayMeColors.TextPrimary) {
    Canvas(modifier) {
        val w = 2.5.dp.toPx()
        val cx = size.width * 0.55f
        drawCircle(color, radius = size.width * 0.1f, center = Offset(cx, size.height * 0.12f))
        drawLine(color, Offset(cx, size.height * 0.25f), Offset(cx - size.width * 0.08f, size.height * 0.6f), strokeWidth = w)
        drawLine(color, Offset(cx - size.width * 0.08f, size.height * 0.6f), Offset(cx - size.width * 0.25f, size.height * 0.95f), strokeWidth = w)
        drawLine(color, Offset(cx - size.width * 0.08f, size.height * 0.6f), Offset(cx + size.width * 0.12f, size.height * 0.95f), strokeWidth = w)
        drawLine(color, Offset(cx - size.width * 0.03f, size.height * 0.35f), Offset(cx - size.width * 0.28f, size.height * 0.5f), strokeWidth = w)
        drawLine(color, Offset(cx - size.width * 0.03f, size.height * 0.35f), Offset(cx + size.width * 0.22f, size.height * 0.48f), strokeWidth = w)
    }
}

/** 문 아이콘(디자인 하단 "뒷문이 열렸다 닫히면…" 안내 앞) */
@Composable
fun DoorIcon(modifier: Modifier = Modifier, color: Color = WayMeColors.TextSecondary) {
    Canvas(modifier) {
        drawRoundRect(color, style = Stroke(2.dp.toPx()), cornerRadius = androidx.compose.ui.geometry.CornerRadius(3.dp.toPx()))
        drawCircle(color, radius = 1.5.dp.toPx(), center = Offset(size.width * 0.72f, size.height * 0.55f))
    }
}

/** 카드 공통 */
@Composable
fun StatusCard(
    title: String,
    subtitle: String?,
    warning: Boolean,
    /** 디자인(픽업 정차): 아이가 나오는 중이면 올리브 테두리 강조 */
    highlighted: Boolean = false,
    icon: @Composable () -> Unit,
) {
    val shape = RoundedCornerShape(20.dp)
    Row(
        Modifier
            .fillMaxWidth()
            .clip(shape)
            .background(if (warning) Color(0xFF3A2020) else WayMeColors.Card)
            .then(
                when {
                    warning -> Modifier.border(1.dp, Color(0xFF5A2E2E), shape)
                    highlighted -> Modifier.border(2.dp, Color(0xFF8FA63A), shape)
                    else -> Modifier
                }
            )
            .padding(horizontal = 24.dp, vertical = 20.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Box(
            Modifier.size(52.dp).clip(RoundedCornerShape(50)).background(if (warning) Color(0xFF4A2424) else WayMeColors.Avatar),
            contentAlignment = Alignment.Center,
        ) { icon() }
        Spacer(Modifier.width(20.dp))
        androidx.compose.foundation.layout.Column {
            Text(title, color = WayMeColors.TextPrimary, fontSize = 22.sp, fontWeight = FontWeight.Bold)
            subtitle?.let { Text(it, color = WayMeColors.TextSecondary, fontSize = 18.sp) }
        }
    }
}

