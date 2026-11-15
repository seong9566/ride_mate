package com.example.pickup_hyundai.ui.theme

import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.darkColorScheme
import androidx.compose.runtime.Composable

private val WayMeColorScheme = darkColorScheme(
    primary = WayMeColors.Accent,
    onPrimary = WayMeColors.OnAccent,
    background = WayMeColors.Background,
    onBackground = WayMeColors.TextPrimary,
    surface = WayMeColors.Panel,
    onSurface = WayMeColors.TextPrimary,
    surfaceVariant = WayMeColors.Card,
    onSurfaceVariant = WayMeColors.TextSecondary,
    outline = WayMeColors.Divider,
    error = WayMeColors.Warning,
)

/** 차량 화면은 디자인상 다크 테마 단일. 동적 색상은 사용하지 않는다. */
@Composable
fun PickUp_HyundaiTheme(content: @Composable () -> Unit) {
    MaterialTheme(
        colorScheme = WayMeColorScheme,
        typography = Typography,
        content = content,
    )
}
