package com.example.pickup_hyundai.ui.components

import androidx.compose.foundation.Canvas
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
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
import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberUpdatedState
import androidx.compose.runtime.setValue
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
import com.example.pickup_hyundai.domain.voice.VoiceCommandParser
import com.example.pickup_hyundai.platform.voice.LocalVoice
import com.example.pickup_hyundai.ui.theme.WayMeColors

/**
 * 화면 하단 음성 바: 마이크 아이콘(탭하면 듣기 시작/중지) + 상태 문구.
 * 최종 인식 문장이 이 화면의 짧은 명령(VoiceCommandParser)이면 [onCommand]로 처리한다.
 * 그 밖의 말은 처리하지 않고 Gleo를 안내한다(질문·복합 요청은 Gleo가 이해해 Gleo Interface로 앱 함수를 호출).
 * @param idleText 듣기 전 문구(예: "음성으로 안내하고 있어요" 또는 "\"보내\" 또는 \"나중에\"라고 말해 주세요")
 * @param onCommand 처리했으면 true
 */
@Composable
fun VoiceCommandBar(
    @Suppress("UNUSED_PARAMETER") screen: String,
    idleText: String,
    onCommand: (VoiceCommand) -> Boolean = { false },
) {
    val voice = LocalVoice.current ?: return
    val state by voice.input.state.collectAsState()
    val handler by rememberUpdatedState(onCommand)
    var message by remember { mutableStateOf<String?>(null) }

    LaunchedEffect(voice) {
        voice.input.finals.collect { text ->
            val cmd = VoiceCommandParser.parse(text)
            message = if (cmd != null && handler(cmd)) "\"$text\" 알아들었어요"
            else "\"$text\"은(는) 여기서 처리할 수 없어요. 일정·경로·아이 연락은 Gleo에게 말해 주세요"
        }
    }
    // 화면을 떠나면 마이크를 놓는다
    DisposableEffect(voice) { onDispose { voice.input.stop() } }

    val listening = state.listening
    val text = when {
        listening -> state.partial?.takeIf { it.isNotBlank() } ?: "듣고 있어요…"
        state.available == false -> state.error ?: "음성 인식을 쓸 수 없어요"
        message != null -> message!!
        state.error != null -> state.error!!
        else -> idleText
    }
    val shape = RoundedCornerShape(50)
    Row(
        Modifier
            .fillMaxWidth()
            .clip(shape)
            .border(1.dp, if (listening) WayMeColors.Accent else WayMeColors.Divider, shape)
            .clickable { message = null; voice.input.toggle() }
            .padding(horizontal = 20.dp, vertical = 14.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Box(
            Modifier.size(52.dp).clip(CircleShape).background(if (listening) WayMeColors.Accent else WayMeColors.Card),
            contentAlignment = Alignment.Center,
        ) { MicIcon(if (listening) WayMeColors.OnAccent else WayMeColors.TextPrimary) }
        Spacer(Modifier.width(20.dp))
        Text(
            text,
            color = if (state.available == false) WayMeColors.TextTertiary else WayMeColors.TextPrimary,
            fontSize = 20.sp, fontWeight = FontWeight.Bold, maxLines = 2, overflow = TextOverflow.Ellipsis,
        )
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
