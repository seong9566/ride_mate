package com.example.pickup_hyundai.ui.components

import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp

private val KeyColor = Color(0xFF6B6D73)
private val KeyColorDark = Color(0xFF3F4146)

/** 차량용 숫자 키패드(디자인: 1~9, 지우기, 0, ⌫). 시스템 키보드 대신 큰 터치 영역 사용 */
@Composable
fun NumberKeypad(
    onDigit: (Char) -> Unit,
    onClear: () -> Unit,
    onBackspace: () -> Unit,
    keyHeight: Dp = 76.dp,
) {
    val rows = listOf(listOf('1', '2', '3'), listOf('4', '5', '6'), listOf('7', '8', '9'))
    Column(verticalArrangement = Arrangement.spacedBy(10.dp)) {
        rows.forEach { row ->
            Row(horizontalArrangement = Arrangement.spacedBy(10.dp)) {
                row.forEach { d -> Key(d.toString(), KeyColor, keyHeight, Modifier.weight(1f)) { onDigit(d) } }
            }
        }
        Row(horizontalArrangement = Arrangement.spacedBy(10.dp)) {
            Key("지우기", KeyColorDark, keyHeight, Modifier.weight(1f), small = true, onClick = onClear)
            Key("0", KeyColor, keyHeight, Modifier.weight(1f)) { onDigit('0') }
            Key("", KeyColorDark, keyHeight, Modifier.weight(1f), icon = { BackspaceIcon() }, onClick = onBackspace)
        }
    }
}

@Composable
private fun Key(
    label: String,
    color: Color,
    height: Dp,
    modifier: Modifier,
    small: Boolean = false,
    icon: (@Composable () -> Unit)? = null,
    onClick: () -> Unit,
) {
    Box(
        modifier
            .height(height)
            .fillMaxWidth()
            .clip(RoundedCornerShape(16.dp))
            .background(color)
            .clickable(onClick = onClick),
        contentAlignment = Alignment.Center,
    ) {
        if (icon != null) icon() else Text(label, color = Color.White, fontSize = if (small) 24.sp else 34.sp, fontWeight = FontWeight.Bold)
    }
}

/** 디자인 지우기(⌫) 아이콘: 왼쪽이 뾰족한 태그 + × */
@Composable
private fun BackspaceIcon() {
    androidx.compose.foundation.Canvas(androidx.compose.ui.Modifier.height(24.dp).fillMaxWidth(0.18f)) {
        val w = size.width; val h = size.height
        val path = androidx.compose.ui.graphics.Path().apply {
            moveTo(w * 0.3f, 0f); lineTo(w, 0f); lineTo(w, h); lineTo(w * 0.3f, h); lineTo(0f, h / 2); close()
        }
        drawPath(path, Color.White)
        val cx = w * 0.62f; val cy = h / 2; val r = h * 0.2f
        val sw = 2.5.dp.toPx()
        drawLine(KeyColorDark, androidx.compose.ui.geometry.Offset(cx - r, cy - r), androidx.compose.ui.geometry.Offset(cx + r, cy + r), sw)
        drawLine(KeyColorDark, androidx.compose.ui.geometry.Offset(cx - r, cy + r), androidx.compose.ui.geometry.Offset(cx + r, cy - r), sw)
    }
}

/** 초기 설정 진행 표시: "아이 1 / 3" + 3칸 막대 */
@Composable
fun SetupProgress(label: String, step: Int, total: Int = 3) {
    Row(verticalAlignment = Alignment.CenterVertically) {
        Text(label, color = com.example.pickup_hyundai.ui.theme.WayMeColors.TextPrimary, fontSize = 18.sp, fontWeight = FontWeight.Bold)
        Text("  $step / $total", color = com.example.pickup_hyundai.ui.theme.WayMeColors.TextSecondary, fontSize = 16.sp)
    }
    androidx.compose.foundation.layout.Spacer(Modifier.height(10.dp))
    Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
        (1..total).forEach { i ->
            val c = when {
                i == step -> com.example.pickup_hyundai.ui.theme.WayMeColors.Accent
                i < step -> Color(0xFF4A5326)
                else -> Color(0xFF45474C)
            }
            Box(Modifier.weight(1f).height(5.dp).clip(RoundedCornerShape(50)).background(c))
        }
    }
}

/** 전화번호 표시용 포맷: 01012345678 → 010-1234-5678 */
fun formatPhone(digits: String): String = when {
    digits.length <= 3 -> digits
    digits.length <= 7 -> "${digits.take(3)}-${digits.drop(3)}"
    else -> "${digits.take(3)}-${digits.substring(3, 7)}-${digits.drop(7)}"
}

/** 010-****-5678 */
fun maskPhone(digits: String): String =
    if (digits.length >= 10) "${digits.take(3)}-****-${digits.takeLast(4)}" else formatPhone(digits)
