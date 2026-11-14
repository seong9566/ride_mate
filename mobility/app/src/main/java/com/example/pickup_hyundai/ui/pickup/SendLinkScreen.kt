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
import androidx.compose.ui.text.SpanStyle
import androidx.compose.ui.text.buildAnnotatedString
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.withStyle
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.example.pickup_hyundai.domain.model.SendLinkPreview
import com.example.pickup_hyundai.ui.components.MapPlaceholder
import com.example.pickup_hyundai.ui.components.PanelHeader
import com.example.pickup_hyundai.ui.components.PrimaryPanelButton
import com.example.pickup_hyundai.ui.components.WayMeScaffold
import com.example.pickup_hyundai.ui.theme.WayMeColors

/**
 * V09 아이에게 픽업 장소 보내기("만남 연결") — 디자인: 4.아이에게 픽업 장소 보내기.png
 *
 * 디자인은 음성("보내" / "나중에")으로만 응답한다. SpeechToText SDK 연동 전까지 같은 자리에
 * 터치 버튼 2개를 임시로 둔다(디자인 외 요소 → 팀 확인 필요).
 */
@Composable
fun SendLinkScreen(
    state: SendLinkUiState,
    onSend: () -> Unit,
    onLater: () -> Unit,
    onRetry: () -> Unit,
    /** 지도 칩용: 아이 도보(분)·도착까지 남은 시간(분). PickupSession에서 전달 */
    walkMin: Int? = null,
    remainMin: Int? = null,
) {
    val load = state.load
    WayMeScaffold(
        map = { MapPanel(MapScenes.sendLink(walkMin, remainMin)) },
        panel = {
            PanelHeader("만남 연결", isMock = (load as? LoadState.Content)?.data?.isMock == true)
            when (load) {
                LoadState.Loading -> CenterLoading()
                is LoadState.Error -> CenterError(load.message, onRetry)
                is LoadState.Content -> Body(load.data, state, onSend, onLater)
            }
        },
    )
}

@Composable
private fun ColumnScope.Body(p: SendLinkPreview, state: SendLinkUiState, onSend: () -> Unit, onLater: () -> Unit) {
    Column(
        Modifier
            .weight(1f)
            .padding(horizontal = 28.dp, vertical = 24.dp),
    ) {
        Text("아이에게 픽업 장소를\n보낼까요?", color = WayMeColors.TextPrimary, fontSize = 32.sp, fontWeight = FontWeight.Bold, lineHeight = 42.sp)
        Spacer(Modifier.height(24.dp))
        Column(
            Modifier
                .fillMaxWidth()
                .clip(RoundedCornerShape(20.dp))
                .background(WayMeColors.Card)
                .padding(24.dp),
        ) {
            Text(
                buildAnnotatedString {
                    withStyle(SpanStyle(color = WayMeColors.TextSecondary)) { append("받는 사람  ") }
                    withStyle(SpanStyle(fontWeight = FontWeight.Bold)) { append("${p.childName} (아이)") }
                },
                color = WayMeColors.TextPrimary, fontSize = 18.sp,
            )
            Spacer(Modifier.height(12.dp))
            Text(p.messageText, color = WayMeColors.TextPrimary, fontSize = 22.sp, fontWeight = FontWeight.Bold)
            Spacer(Modifier.height(16.dp))
            Row(
                Modifier
                    .fillMaxWidth()
                    .clip(RoundedCornerShape(14.dp))
                    .background(Color(0xFF33353A))
                    .padding(16.dp),
                verticalAlignment = Alignment.CenterVertically,
            ) {
                Box(Modifier.size(44.dp).clip(RoundedCornerShape(10.dp)).background(WayMeColors.Accent), contentAlignment = Alignment.Center) {
                    Canvas(Modifier.size(18.dp)) {
                        drawCircle(WayMeColors.OnAccent, radius = size.width * 0.38f, center = Offset(size.width / 2, size.height * 0.4f))
                        drawCircle(WayMeColors.Accent, radius = size.width * 0.14f, center = Offset(size.width / 2, size.height * 0.4f))
                    }
                }
                Spacer(Modifier.width(16.dp))
                Column {
                    Text("라이드메이트 픽업 안내", color = WayMeColors.TextPrimary, fontSize = 18.sp, fontWeight = FontWeight.Bold)
                    Text("픽업 장소와 걷는 길 보기 ›", color = WayMeColors.Accent, fontSize = 16.sp)
                }
            }
        }
        Spacer(Modifier.height(24.dp))
        DeliveryStepper(p.delivery)
        Spacer(Modifier.height(12.dp))
        Text("ⓘ  설정에서 자동 발송을 켜면 묻지 않고 보내요", color = WayMeColors.TextSecondary, fontSize = 16.sp)
        state.sendError?.let {
            Spacer(Modifier.height(8.dp))
            Text(it, color = WayMeColors.Warning, fontSize = 18.sp)
        }
        Spacer(Modifier.weight(1f))
        Text("\"보내\" 또는 \"나중에\"라고 말해 주세요 (음성 연동 예정)", color = WayMeColors.TextTertiary, fontSize = 16.sp, modifier = Modifier.align(Alignment.CenterHorizontally))
        Spacer(Modifier.height(12.dp))
        Row(horizontalArrangement = Arrangement.spacedBy(12.dp)) {
            Box(Modifier.weight(1f)) { PrimaryPanelButton("나중에", onLater, enabled = !state.sending) }
            Box(Modifier.weight(1f)) { PrimaryPanelButton(if (state.sending) "보내는 중…" else "보내기", onSend, enabled = !state.sending) }
        }
    }
}
