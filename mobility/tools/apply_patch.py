#!/usr/bin/env python3
"""
WAY:ME 지도(카카오맵) + 실시간 위치/ETA + 시연 경로 패치를 ride_mate/mobility에 적용한다.
- 새 파일 복사 + 기존 파일 문자열 치환. 치환 대상 문자열이 없으면 그 파일은 건드리지 않고 알려 준다.
- 사용: python3 tools/apply_patch.py <mobility_patch 폴더> <ride_mate/mobility 폴더> [--dry-run]
"""
import os
import shutil
import sys

PATCH, PROJ = sys.argv[1], sys.argv[2]
DRY = "--dry-run" in sys.argv
PKG = "app/src/main/java/com/example/pickup_hyundai"
problems = []


def copy_tree():
    for base, _, files in os.walk(os.path.join(PATCH, "app")):
        for f in files:
            src = os.path.join(base, f)
            rel = os.path.relpath(src, PATCH)
            dst = os.path.join(PROJ, rel)
            print("  + " + rel)
            if not DRY:
                os.makedirs(os.path.dirname(dst), exist_ok=True)
                shutil.copyfile(src, dst)


def edit(rel, pairs, imports=()):
    path = os.path.join(PROJ, rel)
    s = open(path, encoding="utf-8").read()
    orig = s
    for a, b in pairs:
        if b in s and a not in s:
            continue  # 이미 적용됨
        if a not in s:
            problems.append(f"{rel}: 찾지 못함 → {a[:60]!r}")
            return
        s = s.replace(a, b, 1)
    for imp in imports:
        line = f"import {imp}\n"
        if line not in s:
            idx = s.index("\nimport ") + 1
            s = s[:idx] + line + s[idx:]
    if s != orig:
        print("  ~ " + rel)
        if not DRY:
            open(path, "w", encoding="utf-8").write(s)


MAP_IMPORTS = ("com.example.pickup_hyundai.map.MapPanel", "com.example.pickup_hyundai.map.MapScenes")

print("1) 새 파일 복사")
copy_tree()

print("2) Gradle·Manifest")
edit("settings.gradle.kts", [(
    '        maven("https://nexus-playground.pleos.ai/repository/maven-releases/")\n',
    '        maven("https://nexus-playground.pleos.ai/repository/maven-releases/")\n'
    '        // 카카오맵 Android SDK v2 (공식 문서 저장소)\n'
    '        maven("https://devrepo.kakao.com/nexus/repository/kakaomap-releases/")\n')])
edit("gradle/libs.versions.toml", [
    ('pleosVehicle = "2.3.2"\n', 'pleosVehicle = "2.3.2"\nkakaoMap = "2.15.2"\n'),
    ('pleos-vehicle = { group = "ai.pleos.playground", name = "Vehicle", version.ref = "pleosVehicle" }\n',
     'pleos-vehicle = { group = "ai.pleos.playground", name = "Vehicle", version.ref = "pleosVehicle" }\n'
     'kakao-map = { group = "com.kakao.maps.open", name = "android", version.ref = "kakaoMap" }\n'
     'androidx-lifecycle-runtime-compose = { group = "androidx.lifecycle", name = "lifecycle-runtime-compose", version.ref = "lifecycleRuntimeKtx" }\n'),
])
edit("app/build.gradle.kts", [
    ('plugins {\n', 'import java.util.Properties\n\nplugins {\n'),
    ('        testInstrumentationRunner = "androidx.test.runner.AndroidJUnitRunner"\n',
     '        testInstrumentationRunner = "androidx.test.runner.AndroidJUnitRunner"\n\n'
     '        // 카카오 네이티브 앱 키: local.properties(커밋 안 됨)의 KAKAO_NATIVE_APP_KEY. 없으면 지도 자리표시\n'
     '        val localProps = Properties().apply {\n'
     '            rootProject.file("local.properties").takeIf { it.exists() }?.inputStream()?.use { load(it) }\n'
     '        }\n'
     '        buildConfigField("String", "KAKAO_NATIVE_APP_KEY", "\\"${localProps.getProperty("KAKAO_NATIVE_APP_KEY", "")}\\"")\n'),
    ('        compose = true\n', '        compose = true\n        buildConfig = true\n'),
    ('    implementation(libs.pleos.fusedlocation)\n',
     '    implementation(libs.pleos.fusedlocation)\n    implementation(libs.kakao.map)\n    implementation(libs.androidx.lifecycle.runtime.compose)\n'),
])
edit("app/src/main/AndroidManifest.xml", [(
    '    <application\n        android:allowBackup="true"',
    '    <application\n        android:name=".WayMeApplication"\n        android:allowBackup="true"')])
keep = os.path.join(PROJ, "app/src/main/keepRules/rules.keep")
if os.path.exists(keep) and "com.kakao.vectormap" not in open(keep).read():
    print("  ~ app/src/main/keepRules/rules.keep")
    if not DRY:
        with open(keep, "a") as f:
            f.write("\n# 카카오맵 SDK(공식 문서)\n-keep class com.kakao.vectormap.** { *; }\n-keep interface com.kakao.vectormap.**\n")

print("3) 화면 지도 연결")
P = PKG
edit(f"{P}/ui/home/HomeScreen.kt", [("map = { MapPlaceholder() },", "map = { MapPanel(MapScenes.overview()) },")], MAP_IMPORTS)
edit(f"{P}/ui/pickup/TodayPickupScreen.kt", [("map = { MapPlaceholder() },", "map = { MapPanel(MapScenes.overview()) },")], MAP_IMPORTS)
edit(f"{P}/ui/pickup/DepartureScreen.kt", [("map = { MapPlaceholder() },", "map = { MapPanel(MapScenes.overview()) },")], MAP_IMPORTS)
edit(f"{P}/ui/pickup/PickupSpotScreen.kt", [("map = { MapPlaceholder() },",
     "map = { MapPanel(MapScenes.pickupSpots((load as? LoadState.Content)?.data, state.selectedRank)) },")], MAP_IMPORTS)
edit(f"{P}/ui/pickup/WaitingPlaceScreen.kt", [("map = { MapPlaceholder() },",
     "map = { MapPanel(MapScenes.waitingPlaces((load as? LoadState.Content)?.data, state.selectedRank)) },")], MAP_IMPORTS)
edit(f"{P}/ui/pickup/SendLinkScreen.kt", [("map = { MapPlaceholder() },", "map = { MapPanel(MapScenes.pickupStop()) },")], MAP_IMPORTS)
edit(f"{P}/ui/pickup/RecordScreen.kt", [("map = { MapPlaceholder() },", "map = { MapPanel(MapScenes.toHome()) },")], MAP_IMPORTS)
edit(f"{P}/ui/pickup/PickupStopScreen.kt", [(
    "            MapPlaceholder()\n            if (info?.inRestrictedZone == true) {",
    "            MapPanel(MapScenes.pickupStop())\n            if (info?.inRestrictedZone == true) {")], MAP_IMPORTS)
edit(f"{P}/ui/pickup/DrivingScreens.kt", [
    ("            MapPlaceholder()\n            data?.let { MapInfoChip(\"대기 장소까지\", it.progress) }",
     "            MapPanel(MapScenes.toWaiting(data?.place), followVehicle = true)\n            data?.let { MapInfoChip(\"대기 장소까지\", it.progress) }"),
    ("            MapPlaceholder()\n            data?.let { MapInfoChip(\"집까지\", it.progress) }",
     "            MapPanel(MapScenes.toHome(), followVehicle = true)\n            data?.let { MapInfoChip(\"집까지\", it.progress) }"),
], MAP_IMPORTS)

print("4) 앱 루트(위치·내비 전역 제공) + 실시간 남은 시간/거리")
edit(f"{P}/ui/navigation/WayMeApp.kt", [
    ("@Composable\nfun WayMeApp(", "@Composable\ninternal fun WayMeAppContent("),
    ("            WaitingDriveScreen(state = state, onRetry = vm::retry)",
     "            WaitingDriveScreen(state = state.withLiveProgress(DemoScenario.TO_WAITING) { d, p -> d.copy(progress = p) }, onRetry = vm::retry)"),
    ("            HomeDriveScreen(state = state, onRetry = vm::refresh)",
     "            HomeDriveScreen(state = state.withLiveProgress(DemoScenario.TO_HOME) { d, p -> d.copy(progress = p) }, onRetry = vm::refresh)"),
], ("com.example.pickup_hyundai.demo.DemoScenario", "com.example.pickup_hyundai.demo.withLiveProgress"))
edit(f"{P}/MainActivity.kt", [
    ("import com.example.pickup_hyundai.ui.navigation.WayMeApp\n", "import com.example.pickup_hyundai.ui.navigation.WayMeRoot\n"),
    ("else WayMeApp(", "else WayMeRoot("),
])

print("5) Mock 좌표를 시연 경로(대치동)와 맞춤 — Mock 데이터임은 그대로(isMock=true)")
edit(f"{P}/data/repository/MockPickupRepository.kt", [
    ('SpotTag.RECOMMENDED, 5, "학원 앞 공사로 덜 붐벼요", 37.4991, 127.0302)', 'SpotTag.RECOMMENDED, 5, "학원 앞 공사로 덜 붐벼요", 37.49460, 127.06200)'),
    ('SpotTag.LAST_TIME, 3, "오늘 22시 붐빔", 37.4995, 127.0285)', 'SpotTag.LAST_TIME, 3, "오늘 22시 붐빔", 37.49545, 127.06050)'),
    ('null, 6, "길 건너야 해요", 37.4982, 127.0296)', 'null, 6, "길 건너야 해요", 37.49395, 127.06085)'),
    ('"약 2,100원", 120, 37.4930, 127.0480)', '"약 2,100원", 120, 37.49360, 127.05780)'),
    ('"약 2,100원", 30, 37.4990, 127.0640)', '"약 2,100원", 30, 37.49720, 127.06520)'),
    ('"약 2,800원", 10, 37.4960, 127.0600)', '"약 2,800원", 10, 37.49610, 127.05920)'),
])
edit(f"{P}/ui/navigation/WayMeApp.kt", [(
    'WaitingPlace(1, "도곡초교 공영주차장", true, 3, "약 2,100원", 120, 37.4930, 127.0480)',
    'WaitingPlace(1, "도곡초교 공영주차장", true, 3, "약 2,100원", 120, 37.49360, 127.05780)')])

print("\n완료" + (" (dry-run: 실제로 쓰지 않음)" if DRY else ""))
if problems:
    print("\n⚠ 적용하지 못한 항목(파일이 예상과 다름 — 직접 확인 필요):")
    for p in problems:
        print("  - " + p)
    sys.exit(2)
