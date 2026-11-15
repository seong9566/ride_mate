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
import androidx.compose.foundation.text.input.InputTransformation
import androidx.compose.foundation.text.input.TextFieldLineLimits
import androidx.compose.foundation.text.input.maxLength
import androidx.compose.foundation.text.input.rememberTextFieldState
import androidx.compose.material3.Switch
import androidx.compose.material3.SwitchDefaults
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.SideEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberUpdatedState
import androidx.compose.runtime.setValue
import androidx.compose.runtime.snapshotFlow
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
    val name = rememberTextFieldState(initialText = state.child.name)
    val updateChild by rememberUpdatedState(onChange)
    LaunchedEffect(name) {
        snapshotFlow { name.text.toString() }.collect { text ->
            updateChild { it.copy(name = text) }
        }
    }
    WayMeScaffold(map = {}, panel = {
        PanelHeader("초기 설정", isMock = state.isMock)
        Column(Modifier.weight(1f).padding(horizontal = 28.dp, vertical = 24.dp)) {
            SetupProgress("아이", 1)
            Spacer(Modifier.height(28.dp))
            Text("아이 정보를 알려주세요", color = WayMeColors.TextPrimary, fontSize = 30.sp, fontWeight = FontWeight.Bold)
            Spacer(Modifier.height(20.dp))
            // Keep the IME's composing text in the editor instead of feeding ViewModel updates back into it.
            FieldFrame("이름", name.text.isEmpty(), "이름") {
                BasicTextField(
                    state = name,
                    inputTransformation = InputTransformation.maxLength(10),
                    lineLimits = TextFieldLineLimits.SingleLine,
                    textStyle = TextStyle(color = WayMeColors.TextPrimary, fontSize = 24.sp),
                    cursorBrush = SolidColor(WayMeColors.Accent),
                    modifier = Modifier.fillMaxWidth(),
                )
            }
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
            PrimaryPanelButton(if (state.busy) "저장 중…" else "다음", {
                val text = name.text.toString()
                onChange { it.copy(name = text) }
                onNext()
            }, enabled = !state.busy)
        }
    })
}

@Composable
private fun Field(label: String, value: String, placeholder: String, keyboard: KeyboardType, onValue: (String) -> Unit) {
    val isText = keyboard == KeyboardType.Text
    var editingValue by remember { mutableStateOf(TextFieldValue(value, selection = TextRange(value.length))) }
    // 한글 IME가 음절을 이어서 조합할 수 있도록 조합 범위와 커서 위치를 보존한다.
    val fieldValue = if (isText && editingValue.text == value) {
        editingValue
    } else {
        TextFieldValue(value, selection = TextRange(value.length))
    }
    SideEffect {
        if (isText) editingValue = fieldValue
    }
    FieldFrame(label, value.isEmpty(), placeholder) {
        BasicTextField(
            value = fieldValue,
            onValueChange = {
                if (isText) editingValue = it
                if (it.text != value) onValue(it.text)
            },
            singleLine = true,
            textStyle = TextStyle(color = WayMeColors.TextPrimary, fontSize = 24.sp),
            cursorBrush = SolidColor(WayMeColors.Accent),
            keyboardOptions = KeyboardOptions(keyboardType = keyboard),
            modifier = Modifier.fillMaxWidth(),
        )
    }
}

@Composable
private fun FieldFrame(label: String, isEmpty: Boolean, placeholder: String, content: @Composable () -> Unit) {
    Text(label, color = WayMeColors.TextSecondary, fontSize = 18.sp, fontWeight = FontWeight.Bold)
    Spacer(Modifier.height(8.dp))
    Box(
        Modifier.fillMaxWidth().height(72.dp).clip(RoundedCornerShape(16.dp)).background(InputBg)
            .border(1.dp, WayMeColors.Divider, RoundedCornerShape(16.dp)).padding(horizontal = 24.dp),
        contentAlignment = Alignment.CenterStart,
    ) {
        if (isEmpty) Text(placeholder, color = WayMeColors.TextTertiary, fontSize = 24.sp)
        content()
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

/**
 * A01-2 학원 추가 — "+ 학원 추가"에서 진입. 디자인 파일이 없어 초기 설정 화면과 같은 구성으로 만들었다.
 * 입력값은 시간표 저장 API의 schedule 항목에 대응: displayName(이름) · academyName/address(장소) · endTime(하원 시각) · days(요일).
 * 장소는 장소 검색 API 연결 전이라 직접 입력(좌표 lat/lng 없음). 픽업 장소(pickupSpot)는 서버가 정해 준다.
 */
@Composable
fun AddAcademyScreen(
    state: SetupUiState,
    onChange: ((com.example.pickup_hyundai.domain.model.AcademyInput) -> com.example.pickup_hyundai.domain.model.AcademyInput) -> Unit,
    onToggleDay: (DayOfWeek) -> Unit,
    onAdjustTime: (Int, Int) -> Unit,
    onSave: () -> Unit,
    onCancel: () -> Unit,
    canUseCurrentLocation: Boolean = false,
    onUseCurrentLocation: () -> Unit = {},
) {
    val a = state.academy
    WayMeScaffold(map = {}, panel = {
        PanelHeader("초기 설정", isMock = state.isMock)
        Column(Modifier.weight(1f).padding(horizontal = 28.dp, vertical = 24.dp)) {
            SetupProgress("시간표", 2)
            Spacer(Modifier.height(24.dp))
            Text("학원을 추가해 주세요", color = WayMeColors.TextPrimary, fontSize = 30.sp, fontWeight = FontWeight.Bold)
            Spacer(Modifier.height(18.dp))
            Field("학원 이름", a.displayName, "수학학원처럼 부르는 이름", KeyboardType.Text) { v -> onChange { it.copy(displayName = v.take(20)) } }
            Spacer(Modifier.height(14.dp))
            Field("학원 장소", a.placeName, "학원 상호 (예: ○○수학학원 대치점)", KeyboardType.Text) { v -> onChange { it.copy(placeName = v.take(40)) } }
            Spacer(Modifier.height(14.dp))
            Field("주소", a.address, "서울 강남구 대치동 …", KeyboardType.Text) { v -> onChange { it.copy(address = v.take(60)) } }
            Spacer(Modifier.height(10.dp))
            // 장소 검색 연결 전: 학원 앞에서 지금 차 위치를 학원 위치로 등록(좌표가 있어야 서버가 픽업 장소·출발 안내를 준다)
            Row(verticalAlignment = Alignment.CenterVertically) {
                Chip(
                    if (a.latitude != null) "학원 위치 등록됨 ✓" else "지금 차 위치를 학원 위치로 등록",
                    selected = a.latitude != null,
                    onClick = { if (canUseCurrentLocation) onUseCurrentLocation() },
                )
                if (a.latitude == null) {
                    Spacer(Modifier.width(12.dp))
                    Text(if (canUseCurrentLocation) "위치가 없으면 픽업 안내가 제한돼요" else "차량 위치 확인 중…", color = WayMeColors.TextTertiary, fontSize = 15.sp)
                }
            }
            Spacer(Modifier.height(18.dp))
            Text("요일", color = WayMeColors.TextSecondary, fontSize = 18.sp, fontWeight = FontWeight.Bold)
            Spacer(Modifier.height(8.dp))
            Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                DayOfWeek.entries.forEach { day ->
                    val on = day in a.days
                    Box(
                        Modifier.weight(1f).height(64.dp).clip(RoundedCornerShape(14.dp))
                            .background(if (on) WayMeColors.Accent else WayMeColors.Card)
                            .clickable { onToggleDay(day) },
                        contentAlignment = Alignment.Center,
                    ) {
                        Text(KstFormat.dayShort(day), color = if (on) WayMeColors.OnAccent else WayMeColors.TextPrimary, fontSize = 20.sp, fontWeight = FontWeight.Bold)
                    }
                }
            }
            Spacer(Modifier.height(18.dp))
            Text("하원 시각", color = WayMeColors.TextSecondary, fontSize = 18.sp, fontWeight = FontWeight.Bold)
            Spacer(Modifier.height(8.dp))
            Row(verticalAlignment = Alignment.CenterVertically) {
                StepButton("−") { onAdjustTime(-1, 0) }
                Text("%02d".format(a.endTime.hour), color = WayMeColors.TextPrimary, fontSize = 40.sp, fontWeight = FontWeight.Bold, modifier = Modifier.padding(horizontal = 16.dp))
                StepButton("+") { onAdjustTime(1, 0) }
                Text(":", color = WayMeColors.TextSecondary, fontSize = 40.sp, fontWeight = FontWeight.Bold, modifier = Modifier.padding(horizontal = 16.dp))
                StepButton("−") { onAdjustTime(0, -5) }
                Text("%02d".format(a.endTime.minute), color = WayMeColors.TextPrimary, fontSize = 40.sp, fontWeight = FontWeight.Bold, modifier = Modifier.padding(horizontal = 16.dp))
                StepButton("+") { onAdjustTime(0, 5) }
            }
            (state.academyError ?: state.error)?.let { Spacer(Modifier.height(10.dp)); Text(it, color = WayMeColors.Warning, fontSize = 18.sp) }
            Spacer(Modifier.weight(1f))
            Row(horizontalArrangement = Arrangement.spacedBy(12.dp)) {
                Box(Modifier.weight(1f)) { PrimaryPanelButton("취소", onCancel, enabled = !state.busy) }
                Box(Modifier.weight(1f)) { PrimaryPanelButton(if (state.busy) "저장 중…" else "저장", onSave, enabled = !state.busy) }
            }
        }
    })
}

/** 시각 조정 버튼(터치 영역 64dp) */
@Composable
private fun StepButton(label: String, onClick: () -> Unit) {
    Box(
        Modifier.size(64.dp).clip(RoundedCornerShape(16.dp)).background(WayMeColors.Card).clickable(onClick = onClick),
        contentAlignment = Alignment.Center,
    ) { Text(label, color = WayMeColors.TextPrimary, fontSize = 30.sp, fontWeight = FontWeight.Bold) }
}

/** A01-3 집 — 디자인: 초기 설정/3.집 주소 검색 및 등록.png */
@Composable
fun HomeSetupScreen(
    state: SetupUiState,
    onUseNaviHome: () -> Unit,
    onSearchOther: () -> Unit,
    onDone: () -> Unit,
    onManualAddress: (String) -> Unit = {},
    /** 차량 위치를 알 때만 "지금 차 위치로 등록" 가능 */
    canUseCurrentLocation: Boolean = false,
    onUseCurrentLocation: () -> Unit = {},
) {
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
                    Chip(if (home == null) "직접 등록" else "다른 곳으로 등록", selected = state.manualHome, onClick = onSearchOther)
                }
            }
            if (state.manualHome || home == null) {
                // 주소 검색 API가 없어: 주소는 직접 입력, 위치는 지금 차 위치(집에 주차한 상태에서 등록)
                Spacer(Modifier.height(16.dp))
                Field("집 주소", state.manualAddress, "예: 서울 강남구 도곡로 …", KeyboardType.Text, onManualAddress)
                Spacer(Modifier.height(12.dp))
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Chip("지금 차 위치를 집으로 등록", selected = false, onClick = { if (canUseCurrentLocation) onUseCurrentLocation() })
                    if (!canUseCurrentLocation) {
                        Spacer(Modifier.width(12.dp))
                        Text("차량 위치 확인 중…", color = WayMeColors.TextTertiary, fontSize = 16.sp)
                    }
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
