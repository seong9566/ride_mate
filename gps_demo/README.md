# WAY:ME 시연용 GPS 시뮬레이션 (DEMO 전용)

> 이 폴더의 경로·좌표·스크립트는 **시연 영상 촬영용**입니다. 실제 차량 위치나 서비스 데이터가 아닙니다.

## 0. 먼저 검증 (5분)

에뮬레이터(Pleos Connect v2.1.0, Cold Boot)를 켠 상태에서:

```bash
# A. 콘솔 명령 동작 확인 — 순서 주의: 경도 먼저, 위도 나중
adb emu geo fix 127.0635 37.4946
adb shell dumpsys location | grep -i -A2 "last location"

# B. 앱이 받는지 확인 (SDK 검증 화면 → FusedLocation 칸)
adb shell am start -n com.example.pickup_hyundai/.MainActivity --ez sdk_check true
adb emu geo fix 127.0529 37.4963      # 값이 바뀌는지 관찰
adb logcat -d | grep -E "WayMe/Location|FusedLocation" | tail -20

# C. 스크립트로 한 번에
python3 gps_demo.py check --lat 37.4946 --lng 127.0635
```

결과를 아래 표에 기록합니다.

| 항목 | 확인 방법 | 결과 |
|---|---|---|
| geo fix → Android 위치 서비스 | 위 A의 dumpsys | |
| geo fix → FusedLocation SDK(앱) | 위 B, 검증 화면 위도·경도 | |
| geo fix → Pleos 기본 내비 현재 위치 | 내비 화면의 차량 아이콘 이동 | |
| Extended Controls GPX 재생 | `...` > Location > Load GPX/KML > Play | |
| Virtual Console 문 열림 + GPS 재생 동시 | 재생 중 Door Position 변경 | |

## 1. 경로 만들기

```bash
# 도로를 따르는 경로(OSRM 공개 데모 서버, 인터넷 필요, 시연 준비용·상용 금지, © OpenStreetMap contributors)
python3 gps_demo.py build daechi_scenario.json --osrm --out route_cache.json --gpx-dir gpx
# 인터넷 없이: 직선 보간(도로를 따르지 않음)
python3 gps_demo.py build daechi_scenario.json --out route_cache.json --gpx-dir gpx
```

좌표는 `daechi_scenario.json`에서 고칩니다(현재 근사값 — 지도에서 확인 필요). 앱의 `demo/DemoScenario.kt`도 같은 값으로 맞춥니다.

## 2. 시연 재생

```bash
python3 gps_demo.py play route_cache.json --interval 1            # 실제 속도
python3 gps_demo.py play route_cache.json --interval 1 --speedup 3 # 3배속(영상 단축)
python3 gps_demo.py --mode nmea play route_cache.json             # 속도·진행방향까지 전달($GPRMC)
python3 gps_demo.py play route_cache.json --leg to_home           # 구간 하나만
```

- 대기 장소에서는 설정한 시간만큼 같은 위치를 계속 보냅니다.
- 픽업 장소에서는 멈추고 안내가 나옵니다: Virtual Console(`adb forward tcp:8088 tcp:8088` → `http://127.0.0.1:8088/`) > Convenience > **Door Position**에서 2열 문을 열었다 닫은 뒤 Enter.
- Virtual Console은 브라우저 화면만 문서화되어 있어 문 이벤트는 수동 조작입니다(자동화 API 미문서화).
