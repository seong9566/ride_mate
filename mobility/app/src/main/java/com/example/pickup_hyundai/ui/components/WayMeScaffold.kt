package com.example.pickup_hyundai.ui.components

import androidx.compose.foundation.Image
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.BoxScope
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.ColumnScope
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxHeight
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.example.pickup_hyundai.R
import com.example.pickup_hyundai.ui.theme.WayMeColors

/**
 * MOCK·시연 데이터 배지 표시 여부. 2026-11-15: 화면에서 숨김(사용자 결정).
 * 주의: 배지만 숨긴 것이며, 서버 API가 없는 값은 여전히 예시 데이터다(claude/WAYME_API_연동현황.md).
 */
object DataBadges { const val SHOW = false }

val PanelShape = RoundedCornerShape(28.dp)

/**
 * 디자인 공통 레이아웃: 좌측 지도 패널 + 우측 앱 패널.
 */
@Composable
fun WayMeScaffold(
    map: @Composable BoxScope.() -> Unit,
    panel: @Composable ColumnScope.() -> Unit,
    modifier: Modifier = Modifier,
) {
    Row(
        modifier = modifier
            .fillMaxSize()
            .background(WayMeColors.Background)
            .padding(16.dp),
        horizontalArrangement = Arrangement.spacedBy(16.dp),
    ) {
        Box(
            modifier = Modifier
                .weight(1f)
                .fillMaxHeight()
                .clip(PanelShape)
                .background(WayMeColors.MapBase),
            content = map,
        )
        Column(
            modifier = Modifier
                .weight(1f)
                .fillMaxHeight()
                .clip(PanelShape)
                .background(WayMeColors.Panel),
            content = panel,
        )
    }
}

/** 우측 패널 상단 헤더: 로고 · 라이드메이트 | 화면명 · ••• */
@Composable
fun PanelHeader(screenName: String, isMock: Boolean = false, onBack: (() -> Unit)? = null) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .padding(horizontal = 28.dp, vertical = 20.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        if (onBack != null) {
            // 디자인(픽업 장소 선택): 하위 화면은 로고 대신 뒤로 가기 표시. 터치 영역 64dp
            Box(
                Modifier
                    .size(64.dp)
                    .clip(RoundedCornerShape(16.dp))
                    .clickable(onClick = onBack),
                contentAlignment = Alignment.Center,
            ) {
                Text("〈", color = WayMeColors.TextPrimary, fontSize = 26.sp, fontWeight = FontWeight.Bold)
            }
        } else {
            Image(
                painter = painterResource(R.drawable.img_wayme_logo),
                contentDescription = "라이드메이트",
                modifier = Modifier
                    .size(48.dp)
                    .clip(RoundedCornerShape(10.dp)),
            )
        }
        Spacer(Modifier.width(16.dp))
        Text("라이드메이트", color = WayMeColors.TextPrimary, fontSize = 24.sp, fontWeight = FontWeight.Bold)
        Text("  |  ", color = WayMeColors.TextTertiary, fontSize = 20.sp)
        Text(screenName, color = WayMeColors.TextPrimary, fontSize = 22.sp)
        if (isMock) {
            Spacer(Modifier.width(12.dp))
            MockBadge()
        }
        Spacer(Modifier.weight(1f))
        // 디자인의 ••• 메뉴. 동작이 정의되지 않아 표시만 한다(USER_FLOW 확정 후 연결).
        Text("•••", color = WayMeColors.TextPrimary, fontSize = 22.sp, fontWeight = FontWeight.Bold)
    }
    HorizontalDivider(color = WayMeColors.Divider)
}

/** Mock 데이터 표시(08_USER_FLOW §4-10: 실데이터와 혼동 금지) */
@Composable
fun MockBadge() {
    if (!DataBadges.SHOW) return
    Text(
        "MOCK",
        color = WayMeColors.OnAccent,
        fontSize = 12.sp,
        fontWeight = FontWeight.Bold,
        modifier = Modifier
            .clip(RoundedCornerShape(6.dp))
            .background(WayMeColors.Warning)
            .padding(horizontal = 8.dp, vertical = 2.dp),
    )
}

/** 지도 SDK 연동 전 자리표시. 실제 지도·마커·경로처럼 보이게 그리지 않는다. */
@Composable
fun BoxScope.MapPlaceholder(caption: String = "지도 영역 · 지도 SDK 연동 예정") {
    Column(
        modifier = Modifier.align(Alignment.Center),
        horizontalAlignment = Alignment.CenterHorizontally,
    ) {
        Text(caption, color = WayMeColors.TextTertiary, fontSize = 18.sp)
        Spacer(Modifier.height(4.dp))
        Text("(외부 지도 라이선스·Pleos 정책 확인 후 적용)", color = WayMeColors.TextTertiary, fontSize = 14.sp)
    }
}

/** 패널 하단 주요 버튼(디자인: 카드색 배경, 높이 76dp) */
@Composable
fun PrimaryPanelButton(text: String, onClick: () -> Unit, enabled: Boolean = true) {
    androidx.compose.material3.Button(
        onClick = onClick,
        enabled = enabled,
        modifier = Modifier
            .fillMaxWidth()
            .height(76.dp),
        shape = RoundedCornerShape(20.dp),
        colors = androidx.compose.material3.ButtonDefaults.buttonColors(
            containerColor = WayMeColors.Card,
            contentColor = WayMeColors.TextPrimary,
            disabledContainerColor = WayMeColors.Card,
            disabledContentColor = WayMeColors.TextTertiary,
        ),
    ) {
        Text(text, fontSize = 22.sp, fontWeight = FontWeight.Bold)
    }
}

@Composable
fun ChildAvatar(name: String) {
    Box(
        Modifier
            .size(52.dp)
            .clip(androidx.compose.foundation.shape.CircleShape)
            .background(WayMeColors.Avatar),
        contentAlignment = Alignment.Center,
    ) {
        Text(name.take(1), color = WayMeColors.TextPrimary, fontSize = 20.sp, fontWeight = FontWeight.Bold)
    }
}
