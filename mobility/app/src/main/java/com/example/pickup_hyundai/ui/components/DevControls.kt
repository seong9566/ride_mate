package com.example.pickup_hyundai.ui.components

import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.BoxScope
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.example.pickup_hyundai.BuildConfig
import com.example.pickup_hyundai.ui.theme.WayMeColors

/**
 * 개발·시연 전용(debug 빌드에서만 보임): 실제 도착/탑승 이벤트 없이 다음 단계로 넘긴다.
 * 에뮬레이터에서 GPS·내비 도착 이벤트가 오지 않을 때 화면 흐름을 확인하기 위한 것. 출시 빌드에는 나타나지 않는다.
 */
@Composable
fun BoxScope.DevAdvanceChip(label: String, onClick: () -> Unit) {
    if (!BuildConfig.DEBUG) return
    Text(
        "DEV · $label",
        color = WayMeColors.TextPrimary, fontSize = 15.sp, fontWeight = FontWeight.Bold,
        modifier = Modifier.align(Alignment.BottomCenter).padding(bottom = 20.dp)
            .clip(RoundedCornerShape(12.dp)).background(Color(0xCC3A3B40))
            .border(1.dp, WayMeColors.Warning, RoundedCornerShape(12.dp))
            .clickable(onClick = onClick).padding(horizontal = 16.dp, vertical = 10.dp),
    )
}
