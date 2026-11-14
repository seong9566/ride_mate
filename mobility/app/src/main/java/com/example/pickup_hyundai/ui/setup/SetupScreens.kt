package com.example.pickup_hyundai.ui.setup

import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.BasicTextField
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material3.Switch
import androidx.compose.material3.SwitchDefaults
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.SolidColor
import androidx.compose.ui.text.TextRange
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.input.TextFieldValue
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.example.pickup_hyundai.core.time.KstFormat
import com.example.pickup_hyundai.domain.model.AcademySchedule
import com.example.pickup_hyundai.ui.components.PanelHeader
import com.example.pickup_hyundai.ui.components.PrimaryPanelButton
import com.example.pickup_hyundai.ui.components.SetupProgress
import com.example.pickup_hyundai.ui.components.WayMeScaffold
import com.example.pickup_hyundai.ui.components.formatPhone
import com.example.pickup_hyundai.ui.theme.WayMeColors
import java.time.DayOfWeek

private val InputBg = Color(0xFF16171A)
private val ToggleGreen = Color(0xFF1ED760)

/** A01-1 아이 정보 — 디자인: 초기 설정/1.아이정보입력.png */
@Composable
fun ChildInfoScreen(state: SetupUiState, onChange: ((com.example.pickup_hyundai.domain.model.ChildInfoInput) -> com.example.pickup_hyundai.domain.model.ChildInfoInput) -> Unit, onNext: () -> Unit) {
    WayMeScaffold(map = {}, panel = {
        PanelHeader("초기 설정", isMock = state.isMock)
        Column(Modifier.weight(1f).padding(horizontal = 28.dp, vertical = 24.dp)) {
            SetupProgress("아이", 1)
            Spacer(Modifier.height(28.dp))
            Text("아이 정보를 알려주세요", color = WayMeColors.TextPrimary, fontSize = 30.sp, fontWeight = FontWeight.Bold)
            Spacer(Modifier.height(20.dp))
            Field("이름", state.child.name, "이름", KeyboardType.Text) { v -> onChange { it.copy(name = v.take(20)) } }
            Spacer(Modifier.height(16.dp))
            Field("휴대폰 번호", formatPhone(state.child.phoneDigits), "010-0000-0000", KeyboardType.Number) { v ->
                onChange { it.copy(phoneDigits = v.filter(Char::isDigit).take(11)) }
            }
            Spacer(Modifier.height(16.dp))
            Field("생년월일", formatBirth(state.child.birthDigits), "2010. 05. 14", KeyboardType.Number) { v ->
                onChange { it.copy(birthDigits = v.filter(Char::isDigit).take(8)) }
            }
            state.childError?.let { Spacer(Modifier.height(12.dp)); Text(it, color = WayMeColors.Warning, fontSize = 18.sp) }
            state.error?.let { Spacer(Modifier.height(12.dp)); Text(it, color = WayMeColors.Warning, fontSize = 18.sp) }
            Spacer(Modifier.weight(1f))
            PrimaryPanelButton(if (state.busy) "저장 중…" else "다음", onNext, enabled = !state.busy)
        }
    })
}

@Composable
private fun Field(label: String, value: String, placeholder: String, keyboard: KeyboardType, onValue: (String) -> Unit) {
    Text(label, color = WayMeColors.TextSecondary, fontSize = 18.sp, fontWeight = FontWeight.Bold)
    Spacer(Modifier.height(8.dp))
    Box(
        Modifier.fillMaxWidth().height(72.dp).clip(RoundedCornerShape(16.dp)).background(InputBg)
            .border(1.dp, WayMeColors.Divider, RoundedCornerShape(16.dp)).padding(horizontal = 24.dp),
        contentAlignment = Alignment.CenterStart,
    ) {
        if (value.isEmpty()) Text(placeholder, color = WayMeColors.TextTertiary, fontSize = 24.sp)
        // 포맷된 값을 다시 넣으므로 커서는 항상 끝으로 둔다
        BasicTextField(
            value = TextFieldValue(value, selection = TextRange(value.length)),
            onValueChange = { onValue(it.text) },
            singleLine = true,
            textStyle = TextStyle(color = WayMeColors.TextPrimary, fontSize = 24.sp),
            cursorBrush = SolidColor(WayMeColors.Accent),
            keyboardOptions = KeyboardOptions(keyboardType = keyboard),
            modifier = Modifier.fillMaxWidth(),
        )
    }
}

private fun formatBirth(d: String): String = when {
    d.length <= 4 -> d
    d.length <= 6 -> "${d.take(4)}. ${d.drop(4)}"
    else -> "${d.take(4)}. ${d.substring(4, 6)}. ${d.drop(6)}"
}

/** A01-2 학원 시간표 — 디자인: 초기 설정/2.학원시간표등록.png */
@Composable
fun TimetableScreen(
    state: SetupUiState,
    onSelectDay: (DayOfWeek) -> Unit,
    onToggle: (String, Boolean) -> Unit,
    onAddAcademy: () -> Unit,
    onNext: () -> Unit,
) {
    WayMeScaffold(map = {}, panel = {
        PanelHeader("초기 설정", isMock = state.isMock)
        Column(Modifier.weight(1f).padding(horizontal = 28.dp, vertical = 24.dp)) {
            SetupProgress("시간표", 2)
            Spacer(Modifier.height(28.dp))
            Text("학원 시간표를 등록해 주세요", color = WayMeColors.TextPrimary, fontSize = 30.sp, fontWeight = FontWeight.Bold)
            Spacer(Modifier.height(20.dp))
            Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                DayOfWeek.entries.forEach { day ->
                    val items = state.schedules.filter { it.day == day && it.pickupEnabled }.sortedBy { it.endTime }
                    val total = state.schedules.count { it.day == day }
                    val selected = day == state.selectedDay
                    Column(
                        Modifier.weight(1f).clip(RoundedCornerShape(14.dp))
                            .background(if (selected) WayMeColors.CardSelected else WayMeColors.Card)
                            .clickable { onSelectDay(day) }.padding(vertical = 14.dp),
                        horizontalAlignment = Alignment.CenterHorizontally,
                    ) {
                        Text(KstFormat.dayShort(day), color = if (selected) WayMeColors.Accent else WayMeColors.TextPrimary, fontSize = 18.sp, fontWeight = FontWeight.Bold)
                        val first = items.firstOrNull()
                        Text(
                            (first?.let { KstFormat.hm(it.endTime) } ?: "–") + if (total > 1) " +${total - 1}" else "",
                            color = if (first == null) WayMeColors.TextTertiary else WayMeColors.TextPrimary, fontSize = 16.sp, fontWeight = FontWeight.Bold,
                        )
                    }
                }
            }
            Spacer(Modifier.height(20.dp))
            val dayItems = state.schedules.filter { it.day == state.selectedDay }
            Text("${KstFormat.dayShort(state.selectedDay)}요일 · 학원 ${dayItems.size}곳", color = WayMeColors.TextSecondary, fontSize = 18.sp, fontWeight = FontWeight.Bold)
            Spacer(Modifier.height(12.dp))
            Column(verticalArrangement = Arrangement.spacedBy(12.dp)) {
                dayItems.forEach { AcademyRow(it, onToggle) }
                Box(
                    Modifier.fillMaxWidth().height(72.dp).clip(RoundedCornerShape(18.dp)).background(WayMeColors.Card).clickable(onClick = onAddAcademy),
                    contentAlignment = Alignment.Center,
                ) { Text("+ 학원 추가", color = WayMeColors.TextPrimary, fontSize = 22.sp, fontWeight = FontWeight.Bold) }
            }
            state.error?.let { Spacer(Modifier.height(8.dp)); Text(it, color = WayMeColors.Warning, fontSize = 18.sp) }
            Spacer(Modifier.weight(1f))
            PrimaryPanelButton("다음 · 집과 차 등록", onNext)
        }
    })
}

@Composable
private fun AcademyRow(a: AcademySchedule, onToggle: (String, Boolean) -> Unit) {
    Row(
        Modifier.fillMaxWidth().clip(RoundedCornerShape(18.dp)).background(WayMeColors.Card).padding(horizontal = 24.dp, vertical = 18.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Column(Modifier.weight(1f)) {
            Text(a.academyName, color = WayMeColors.TextPrimary, fontSize = 22.sp, fontWeight = FontWeight.Bold)
            Text(a.address, color = WayMeColors.TextSecondary, fontSize = 18.sp)
        }
        Column(horizontalAlignment = Alignment.End) {
            Text(KstFormat.hm(a.endTime), color = if (a.pickupEnabled) WayMeColors.TextPrimary else WayMeColors.TextTertiary, fontSize = 26.sp, fontWeight = FontWeight.Bold)
            Text(if (a.pickupEnabled) "픽업" else "픽업 안 함", color = WayMeColors.TextSecondary, fontSize = 16.sp)
        }
        Spacer(Modifier.width(16.dp))
        Switch(
            checked = a.pickupEnabled,
            onCheckedChange = { onToggle(a.id, it) },
            colors = SwitchDefaults.colors(checkedTrackColor = ToggleGreen, checkedThumbColor = Color.White),
        )
    }
}

/** A01-3 집 — 디자인: 초기 설정/3.집 주소 검색 및 등록.png */
@Composable
fun HomeSetupScreen(state: SetupUiState, onUseNaviHome: () -> Unit, onSearchOther: () -> Unit, onDone: () -> Unit) {
    WayMeScaffold(map = {}, panel = {
        PanelHeader("초기 설정", isMock = state.isMock)
        Column(Modifier.weight(1f).padding(horizontal = 28.dp, vertical = 24.dp)) {
            SetupProgress("집", 3)
            Spacer(Modifier.height(28.dp))
            Text("집을 확인해 주세요", color = WayMeColors.TextPrimary, fontSize = 30.sp, fontWeight = FontWeight.Bold)
            Spacer(Modifier.height(20.dp))
            val home = state.naviHome
            Column(Modifier.fillMaxWidth().clip(RoundedCornerShape(20.dp)).background(WayMeColors.Card).padding(24.dp)) {
                if (home == null) {
                    Text("내비에 저장된 '집'이 없어요", color = WayMeColors.TextSecondary, fontSize = 18.sp)
                } else {
                    Text(if (home.fromNaviBookmark) "내비에 저장된 '집'" else "선택한 주소", color = WayMeColors.TextSecondary, fontSize = 17.sp)
                    Spacer(Modifier.height(6.dp))
                    Text(home.address, color = WayMeColors.TextPrimary, fontSize = 22.sp, fontWeight = FontWeight.Bold)
                }
                Spacer(Modifier.height(14.dp))
                Row(horizontalArrangement = Arrangement.spacedBy(10.dp)) {
                    if (home != null) Chip("이걸로 할게요", selected = state.homeConfirmed, onClick = onUseNaviHome)
                    Chip("다른 곳 검색", selected = false, onClick = onSearchOther)
                }
            }
            state.error?.let { Spacer(Modifier.height(8.dp)); Text(it, color = WayMeColors.Warning, fontSize = 18.sp) }
            Spacer(Modifier.weight(1f))
            PrimaryPanelButton(if (state.busy) "저장 중…" else "완료 · 라이드메이트 시작", onDone, enabled = state.homeConfirmed && !state.busy)
        }
    })
}

@Composable
private fun Chip(text: String, selected: Boolean, onClick: () -> Unit) {
    Text(
        text, color = WayMeColors.TextPrimary, fontSize = 18.sp, fontWeight = FontWeight.Bold,
        modifier = Modifier.clip(RoundedCornerShape(12.dp))
            .background(if (selected) Color(0xFF5A5C62) else Color(0xFF3A3B40))
            .then(if (selected) Modifier.border(1.5.dp, WayMeColors.Accent, RoundedCornerShape(12.dp)) else Modifier)
            .clickable(onClick = onClick).padding(horizontal = 18.dp, vertical = 12.dp),
    )
}

/** A01-4 준비 완료 — 디자인: 초기 설정/4.준비완료.png */
@Composable
fun SetupDoneScreen(state: SetupUiState, onGoHome: () -> Unit) {
    WayMeScaffold(map = {}, panel = {
        PanelHeader("초기 설정", isMock = state.isMock)
        Column(Modifier.weight(1f).padding(horizontal = 28.dp, vertical = 24.dp)) {
            Box(Modifier.size(44.dp).clip(CircleShape).background(WayMeColors.Accent), contentAlignment = Alignment.Center) {
                Text("✓", color = WayMeColors.OnAccent, fontSize = 22.sp, fontWeight = FontWeight.Bold)
            }
            Spacer(Modifier.height(20.dp))
            Text("준비가 끝났어요", color = WayMeColors.TextPrimary, fontSize = 32.sp, fontWeight = FontWeight.Bold)
            Text("앞으로 앱을 열면 그날 하원 일정을 먼저 보여 드려요", color = WayMeColors.TextSecondary, fontSize = 20.sp)
            state.summary?.let { s ->
                Spacer(Modifier.height(20.dp))
                SummaryCard("이번 주 픽업", s.weeklyRows)
                Spacer(Modifier.height(16.dp))
                if (s.pickupPlaceRows.isNotEmpty()) SummaryCard("우리 픽업 장소", s.pickupPlaceRows)
            }
            Spacer(Modifier.height(16.dp))
            Text("바꾸고 싶으면 메뉴의 시간표 관리 · 픽업 장소 바꾸기에서 언제든 바꿀 수 있어요", color = WayMeColors.TextTertiary, fontSize = 16.sp)
            Spacer(Modifier.weight(1f))
            PrimaryPanelButton("홈으로 가기", onGoHome)
        }
    })
}

@Composable
private fun SummaryCard(title: String, rows: List<Pair<String, String>>) {
    Column(Modifier.fillMaxWidth().clip(RoundedCornerShape(20.dp)).background(WayMeColors.Card).padding(24.dp)) {
        Text(title, color = WayMeColors.TextSecondary, fontSize = 17.sp, fontWeight = FontWeight.Bold)
        Spacer(Modifier.height(10.dp))
        rows.forEach { (l, r) ->
            Row(Modifier.fillMaxWidth().padding(vertical = 4.dp)) {
                Text(l, color = WayMeColors.TextPrimary, fontSize = 20.sp, modifier = Modifier.weight(1f))
                Text(r, color = WayMeColors.TextPrimary, fontSize = 20.sp)
            }
        }
    }
}
