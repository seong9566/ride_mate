package com.example.pickup_hyundai.ui.components

import androidx.compose.foundation.Canvas
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
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
import androidx.compose.ui.geometry.CornerRadius
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.geometry.Size
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.example.pickup_hyundai.domain.voice.VoiceCommand
import com.example.pickup_hyundai.ui.theme.WayMeColors

/**
 * 화면 하단 음성 바 — 현재는 **상태 표시만** 한다(누르는 동작 없음, 2026-11-15 결정).
 * 음성 인식(Pleos SpeechToText) 연결 코드는 mobility/disabled_voice/ 에 보관(빌드 제외). 다시 붙일 때 [onCommand]로 명령을 전달한다.
 */
@Composable
fun VoiceCommandBar(
    @Suppress("UNUSED_PARAMETER") screen: String,
    idleText: String,
    @Suppress("UNUSED_PARAMETER") onCommand: (VoiceCommand) -> Boolean = { false },
) {
    val shape = RoundedCornerShape(50)
    Row(
        Modifier.fillMaxWidth().clip(shape).border(1.dp, WayMeColors.Divider, shape).padding(horizontal = 20.dp, vertical = 14.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Box(Modifier.size(52.dp).clip(CircleShape).background(WayMeColors.Card), contentAlignment = Alignment.Center) {
            MicIcon(WayMeColors.TextPrimary)
        }
        Spacer(Modifier.width(20.dp))
        Text(idleText, color = WayMeColors.TextPrimary, fontSize = 20.sp, fontWeight = FontWeight.Bold, maxLines = 2, overflow = TextOverflow.Ellipsis)
    }
}

@Composable
private fun MicIcon(color: Color) {
    Canvas(Modifier.size(width = 18.dp, height = 26.dp)) {
        val w = 2.5.dp.toPx()
        val bodyW = size.width * 0.55f
        drawRoundRect(
            color, topLeft = Offset((size.width - bodyW) / 2, 0f), size = Size(bodyW, size.height * 0.6f),
            cornerRadius = CornerRadius(bodyW / 2),
        )
        drawArc(
            color, 0f, 180f, false, topLeft = Offset(0f, size.height * 0.2f), size = Size(size.width, size.height * 0.6f),
            style = Stroke(w),
        )
        drawLine(color, Offset(size.width / 2, size.height * 0.8f), Offset(size.width / 2, size.height), w)
    }
}
