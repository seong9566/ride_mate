package com.example.pickup_hyundai.ui.auth

import androidx.compose.foundation.Canvas
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.aspectRatio
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
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
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.asImageBitmap
import androidx.compose.ui.graphics.StrokeCap
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.example.pickup_hyundai.domain.model.LoginAccount
import com.example.pickup_hyundai.ui.components.DevAdvanceChip
import com.example.pickup_hyundai.ui.components.NumberKeypad
import com.example.pickup_hyundai.ui.components.PanelHeader
import com.example.pickup_hyundai.ui.components.PrimaryPanelButton
import com.example.pickup_hyundai.ui.components.WayMeScaffold
import com.example.pickup_hyundai.ui.components.formatPhone
import com.example.pickup_hyundai.ui.components.maskPhone
import com.example.pickup_hyundai.ui.theme.WayMeColors

private val InputBg = Color(0xFF16171A)
private val BoxBg = Color(0xFF2C2D31)
private val TimerRed = Color(0xFFFF5A36)

private fun mmss(sec: Int, pad: Boolean = false): String =
    if (pad) "%02d:%02d".format(sec / 60, sec % 60) else "%d:%02d".format(sec / 60, sec % 60)

/**
 * A00 QR 로그인 — 디자인: 로그인/QR로그인.png(2026-11-14 개정)
 * 왼쪽 패널은 디자인상 비어 있음(Portrait 자리). 헤더는 로고 · 라이드메이트 | 로그인.
 */
@Composable
fun QrLoginScreen(
    state: AuthUiState,
    onAccount: (LoginAccount) -> Unit,
    onPhoneLogin: () -> Unit,
    onLogout: () -> Unit,
    onRefreshQr: () -> Unit = {},
    onQrScannedDemo: () -> Unit = {},
) {
    WayMeScaffold(map = { if (state.isMock) DevAdvanceChip("QR 진행(시연)", onQrScannedDemo) }, panel = {
        PanelHeader("로그인", isMock = state.isMock)
        Column(Modifier.weight(1f).padding(horizontal = 28.dp, vertical = 24.dp)) {
            Text("휴대폰으로 QR을 찍어 로그인해 주세요", color = WayMeColors.TextPrimary, fontSize = 28.sp, fontWeight = FontWeight.Bold)
            Spacer(Modifier.height(20.dp))
            Row(verticalAlignment = Alignment.CenterVertically) {
                // M-01 verification_uri_complete로 만든 QR(device_code는 넣지 않음)
                Box(
                    Modifier.size(170.dp).clip(RoundedCornerShape(14.dp)).background(Color.White).padding(8.dp),
                    contentAlignment = Alignment.Center,
                ) {
                    val content = state.qrContent
                    if (content != null && !state.isMock) QrImage(content, Modifier.fillMaxSize())
                    else Text(if (state.isMock) "QR\n(Mock)" else "QR을 만드는 중…", color = Color(0xFF555555), fontSize = 14.sp, textAlign = TextAlign.Center)
                }
                Spacer(Modifier.width(28.dp))
                Column(verticalArrangement = Arrangement.spacedBy(16.dp)) {
                    listOf("휴대폰 카메라로 찍기", "라이드메이트에서 로그인", "차와 자동으로 연결").forEachIndexed { i, t ->
                        Text("${i + 1}   $t", color = WayMeColors.TextPrimary, fontSize = 19.sp)
                    }
                }
            }
            Spacer(Modifier.height(10.dp))
            Row(verticalAlignment = Alignment.CenterVertically) {
                Text("남은 시간 ", color = WayMeColors.TextSecondary, fontSize = 18.sp)
                Text(
                    if (state.qrRemainSec > 0) mmss(state.qrRemainSec, pad = true) else "만료",
                    color = TimerRed, fontSize = 18.sp, fontWeight = FontWeight.Bold,
                )
                Spacer(Modifier.width(8.dp))
                Box(
                    Modifier.size(36.dp).clip(CircleShape).clickable(onClick = onRefreshQr),
                    contentAlignment = Alignment.Center,
                ) { RefreshIcon() }
            }
            val notice = when (state.qrStatus) {
                "SCANNED" -> "휴대폰에서 QR을 찍었어요. 휴대폰에서 로그인을 이어가 주세요"
                else -> state.qrNotice
            }
            notice?.let {
                Spacer(Modifier.height(6.dp))
                Text(it, color = if (state.qrStatus == "SCANNED") WayMeColors.Accent else WayMeColors.TextSecondary, fontSize = 17.sp)
            }
            if (state.accounts.isNotEmpty()) {
                Spacer(Modifier.height(20.dp))
                Text("이 차에 로그인한 계정", color = WayMeColors.TextPrimary, fontSize = 18.sp, fontWeight = FontWeight.Bold)
                Spacer(Modifier.height(12.dp))
                Column(verticalArrangement = Arrangement.spacedBy(12.dp)) {
                    state.accounts.forEachIndexed { i, a -> AccountRow(a, highlighted = i == 0, enabled = !state.busy) { onAccount(a) } }
                }
            }
            state.error?.let { Spacer(Modifier.height(8.dp)); Text(it, color = WayMeColors.Warning, fontSize = 18.sp) }
            Spacer(Modifier.weight(1f))
            PrimaryPanelButton("휴대폰 번호로 로그인", onPhoneLogin)
            Spacer(Modifier.height(4.dp))
            Text(
                "이 차에서 로그아웃", color = TimerRed, fontSize = 20.sp, fontWeight = FontWeight.Bold,
                modifier = Modifier.align(Alignment.CenterHorizontally).clip(RoundedCornerShape(12.dp))
                    .clickable(enabled = !state.busy, onClick = onLogout).padding(horizontal = 24.dp, vertical = 12.dp),
            )
        }
    })
}

@Composable
private fun AccountRow(a: LoginAccount, highlighted: Boolean, enabled: Boolean, onClick: () -> Unit) {
    val shape = RoundedCornerShape(20.dp)
    Row(
        Modifier.fillMaxWidth().clip(shape).background(BoxBg).border(1.dp, WayMeColors.Divider, shape)
            .clickable(enabled = enabled, onClick = onClick).padding(horizontal = 24.dp, vertical = 18.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        // 디자인: 가장 최근 계정은 라임, 나머지는 회색 원
        Box(Modifier.size(52.dp).clip(CircleShape).background(if (highlighted) WayMeColors.Accent else WayMeColors.Avatar))
        Spacer(Modifier.width(20.dp))
        Column(Modifier.weight(1f)) {
            Text(a.userName, color = WayMeColors.TextPrimary, fontSize = 22.sp, fontWeight = FontWeight.Bold)
            Text(
                when {
                    !a.canResume -> "QR로 다시 로그인 필요"
                    a.recentlyUsed -> "최근 사용"
                    else -> "이 차에 로그인됨"
                },
                color = WayMeColors.TextSecondary, fontSize = 18.sp,
            )
        }
        ChevronIcon()
    }
}

/** A00 휴대폰 로그인 — 디자인: 로그인/휴대폰 로그인.png(2026-11-14 개정). 뒤로 가기는 시스템 뒤로 가기 사용 */
@Composable
fun PhoneLoginScreen(
    state: AuthUiState,
    onDigit: (Char) -> Unit,
    onBackspace: () -> Unit,
    onClear: () -> Unit,
    onRequestOtp: () -> Unit,
    @Suppress("UNUSED_PARAMETER") onBack: () -> Unit,
) {
    WayMeScaffold(map = {}, panel = {
        PanelHeader("로그인", isMock = state.isMock)
        Column(Modifier.weight(1f).padding(horizontal = 28.dp, vertical = 24.dp)) {
            Text("휴대폰 번호를 입력해 주세요", color = WayMeColors.TextPrimary, fontSize = 28.sp, fontWeight = FontWeight.Bold)
            Spacer(Modifier.height(20.dp))
            Row(
                Modifier.fillMaxWidth().height(80.dp).clip(RoundedCornerShape(16.dp)).background(InputBg)
                    .border(2.dp, Color.White, RoundedCornerShape(16.dp)).padding(horizontal = 24.dp),
                verticalAlignment = Alignment.CenterVertically,
            ) {
                Text(
                    if (state.phoneDigits.isEmpty()) "010-0000-0000" else formatPhone(state.phoneDigits) + "|",
                    color = if (state.phoneDigits.isEmpty()) WayMeColors.TextTertiary else WayMeColors.TextPrimary,
                    fontSize = 26.sp, modifier = Modifier.weight(1f),
                )
                if (state.phoneDigits.isNotEmpty()) {
                    Box(
                        Modifier.size(40.dp).clip(CircleShape).background(Color(0xFFDADCE0)).clickable(onClick = onClear),
                        contentAlignment = Alignment.Center,
                    ) { Text("✕", color = Color(0xFF111111), fontSize = 18.sp, fontWeight = FontWeight.Bold) }
                }
            }
            Spacer(Modifier.height(12.dp))
            Text(state.error ?: "이 번호로 인증번호를 문자로 보내 드려요", color = if (state.error != null) WayMeColors.Warning else WayMeColors.TextSecondary, fontSize = 17.sp)
            Spacer(Modifier.height(20.dp))
            NumberKeypad(onDigit, onClear, onBackspace)
            Spacer(Modifier.weight(1f))
            PrimaryPanelButton(if (state.busy) "보내는 중…" else "인증번호 받기", onRequestOtp, enabled = state.phoneDigits.length == 11 && !state.busy)
        }
    })
}

/** A00 인증번호 입력 — 디자인: 로그인/인증번호받기.png(2026-11-14 개정) */
@Composable
fun OtpScreen(
    state: AuthUiState,
    onDigit: (Char) -> Unit,
    onBackspace: () -> Unit,
    onClear: () -> Unit,
    onResend: () -> Unit,
    onConfirm: () -> Unit,
    @Suppress("UNUSED_PARAMETER") onBack: () -> Unit,
) {
    WayMeScaffold(map = {}, panel = {
        PanelHeader("로그인", isMock = state.isMock)
        Column(Modifier.weight(1f).padding(horizontal = 28.dp, vertical = 24.dp)) {
            Text("인증번호 6자리를 입력해 주세요", color = WayMeColors.TextPrimary, fontSize = 28.sp, fontWeight = FontWeight.Bold)
            Spacer(Modifier.height(6.dp))
            Text("${maskPhone(state.phoneDigits)}로 보냈어요", color = WayMeColors.TextSecondary, fontSize = 18.sp)
            Spacer(Modifier.height(20.dp))
            Row(horizontalArrangement = Arrangement.spacedBy(10.dp)) {
                (0 until 6).forEach { i ->
                    val ch = state.otp.getOrNull(i)
                    val focused = i == state.otp.length
                    val shape = RoundedCornerShape(16.dp)
                    Box(
                        Modifier.weight(1f).aspectRatio(1f).clip(shape)
                            .background(if (focused) BoxBg else InputBg)
                            .border(if (focused) 3.dp else 1.dp, if (focused) Color.White else WayMeColors.Divider, shape),
                        contentAlignment = Alignment.Center,
                    ) { Text(ch?.toString() ?: if (focused) "|" else "", color = WayMeColors.TextPrimary, fontSize = 34.sp, fontWeight = FontWeight.Bold) }
                }
            }
            Spacer(Modifier.height(14.dp))
            Row(verticalAlignment = Alignment.CenterVertically) {
                val sec = state.otpRemainSec
                Text(
                    if (sec > 0) "남은 시간 ${mmss(sec)}" else "인증번호가 만료됐어요",
                    color = if (sec > 0) WayMeColors.Accent else WayMeColors.Warning, fontSize = 18.sp, fontWeight = FontWeight.Bold,
                    modifier = Modifier.weight(1f),
                )
                Text(
                    "다시 받기", color = WayMeColors.TextPrimary, fontSize = 17.sp, fontWeight = FontWeight.Bold,
                    modifier = Modifier.clip(RoundedCornerShape(12.dp)).background(BoxBg)
                        .clickable(enabled = !state.busy, onClick = onResend).padding(horizontal = 20.dp, vertical = 12.dp),
                )
            }
            state.error?.let { Text(it, color = WayMeColors.Warning, fontSize = 17.sp) }
            Spacer(Modifier.height(16.dp))
            NumberKeypad(onDigit, onClear, onBackspace, keyHeight = 70.dp)
            Spacer(Modifier.weight(1f))
            PrimaryPanelButton(if (state.busy) "확인 중…" else "확인", onConfirm, enabled = state.otp.length == 6 && !state.busy)
        }
    })
}

/**
 * A00 휴대폰 확인 대기 — 디자인: 로그인/휴대폰에서 확인하고 있어요.png(2026-11-14 신규)
 * 차와 휴대폰에 같은 4자리 숫자를 보여 주고 휴대폰에서 '로그인 허용'을 누르면 로그인된다.
 * 차량은 M-02 폴링(qr_status=VERIFIED → 허용 대기)을 계속하고, 허용되면 토큰을 받아 로그인한다.
 */
@Composable
fun LoginConfirmScreen(
    state: AuthUiState,
    onCancel: () -> Unit,
    onApprovedDemo: () -> Unit,
) {
    WayMeScaffold(map = { if (state.isMock) DevAdvanceChip("휴대폰에서 허용(시연)", onApprovedDemo) }, panel = {
        PanelHeader("로그인", isMock = state.isMock)
        Column(Modifier.weight(1f).padding(horizontal = 28.dp, vertical = 24.dp)) {
            Text("휴대폰에서 확인하고 있어요", color = WayMeColors.TextPrimary, fontSize = 28.sp, fontWeight = FontWeight.Bold)
            Spacer(Modifier.height(20.dp))
            val shape = RoundedCornerShape(24.dp)
            Column(
                Modifier.fillMaxWidth().clip(shape).background(BoxBg).border(2.dp, Color.White, shape).padding(vertical = 28.dp, horizontal = 20.dp),
                horizontalAlignment = Alignment.CenterHorizontally,
            ) {
                Text("휴대폰에 보이는 숫자와 같나요?", color = WayMeColors.TextPrimary, fontSize = 18.sp, fontWeight = FontWeight.Bold)
                Spacer(Modifier.height(12.dp))
                Text(
                    state.matchCode?.toCharArray()?.joinToString(" ") ?: "- - - -",
                    color = WayMeColors.TextPrimary, fontSize = 80.sp, fontWeight = FontWeight.Bold, letterSpacing = 6.sp,
                )
                Spacer(Modifier.height(12.dp))
                Text("같으면 휴대폰에서 '로그인 허용'을 눌러 주세요", color = WayMeColors.TextSecondary, fontSize = 18.sp)
            }
            Spacer(Modifier.height(20.dp))
            Row(
                Modifier.fillMaxWidth().clip(RoundedCornerShape(20.dp)).background(BoxBg).border(1.dp, WayMeColors.Divider, RoundedCornerShape(20.dp))
                    .padding(horizontal = 28.dp, vertical = 22.dp),
                verticalAlignment = Alignment.CenterVertically,
            ) {
                Text("남은 시간", color = WayMeColors.TextSecondary, fontSize = 20.sp, modifier = Modifier.weight(1f))
                val sec = state.qrRemainSec
                Text(
                    if (sec > 0) mmss(sec) else "만료",
                    color = if (sec > 0) WayMeColors.Accent else WayMeColors.Warning, fontSize = 30.sp, fontWeight = FontWeight.Bold,
                )
            }
            state.error?.let { Spacer(Modifier.height(8.dp)); Text(it, color = WayMeColors.Warning, fontSize = 18.sp) }
            Spacer(Modifier.weight(1f))
            PrimaryPanelButton("취소", onCancel)
        }
    })
}

@Composable
private fun ChevronIcon() {
    Canvas(Modifier.size(width = 14.dp, height = 24.dp)) {
        val w = 3.dp.toPx()
        drawLine(Color.White, Offset(0f, 0f), Offset(size.width, size.height / 2), w, cap = StrokeCap.Round)
        drawLine(Color.White, Offset(size.width, size.height / 2), Offset(0f, size.height), w, cap = StrokeCap.Round)
    }
}

@Composable
private fun RefreshIcon() {
    Canvas(Modifier.size(22.dp)) {
        val w = 2.5.dp.toPx()
        drawArc(Color.White, 30f, 300f, false, style = Stroke(w, cap = StrokeCap.Round))
        // 화살촉
        val tip = Offset(size.width * 0.93f, size.height * 0.25f)
        drawLine(Color.White, tip, Offset(tip.x, tip.y + size.height * 0.28f), w, cap = StrokeCap.Round)
        drawLine(Color.White, tip, Offset(tip.x - size.width * 0.28f, tip.y), w, cap = StrokeCap.Round)
    }
}

/** QR 이미지(ZXing). 내용이 바뀔 때만 다시 만든다 */
@Composable
private fun QrImage(content: String, modifier: Modifier = Modifier) {
    val bitmap = androidx.compose.runtime.remember(content) {
        runCatching {
            val size = 512
            val m = com.google.zxing.qrcode.QRCodeWriter().encode(
                content, com.google.zxing.BarcodeFormat.QR_CODE, size, size,
                mapOf(com.google.zxing.EncodeHintType.MARGIN to 0),
            )
            val px = IntArray(size * size) { i -> if (m.get(i % size, i / size)) android.graphics.Color.BLACK else android.graphics.Color.WHITE }
            android.graphics.Bitmap.createBitmap(px, size, size, android.graphics.Bitmap.Config.ARGB_8888).asImageBitmap()
        }.getOrNull()
    }
    if (bitmap != null) androidx.compose.foundation.Image(bitmap, contentDescription = "로그인 QR", modifier = modifier)
    else Text("QR을 만들지 못했어요", color = Color(0xFF555555), fontSize = 14.sp)
}
