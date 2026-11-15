# 라이드메이트 (RideMate)

> 자녀의 하원 상태와 보호자의 차량 이동 정보를 실시간으로 연결하고, 사전에 검증된 승하차 장소 중 보행 안전성·차량 접근성·예상 혼잡도·도착 시간을 고려해 최적의 픽업 장소와 출발 시각을 추천하는 차량용 앱

현대자동차 Pleos Connect 차량용 앱 개발 공모전 출품작입니다. 차량 앱과 백엔드 서버를 한 저장소에서 관리합니다.

## 목차

1. [주요 기능](#주요-기능)
2. [구성](#구성)
3. [실행 방법](#실행-방법)
4. [테스트 환경 및 필요한 설정](#테스트-환경-및-필요한-설정)
5. [Pleos Connect 에뮬레이터 버전과 화면 규격](#pleos-connect-에뮬레이터-버전과-화면-규격)
6. [APK](#apk)
7. [팀 구성](#팀-구성)

## 주요 기능

| 기능 | 설명 |
|---|---|
| 출발 시각 추천 | 하원 시각, 현재 위치, 실시간 도로 소통, 학원가 혼잡 이력으로 언제 출발할지 계산합니다 |
| 픽업 장소 추천 | 학원 주변에 미리 검증한 승하차 장소 중 어린이보호구역을 피하고 도보·차량 접근이 좋은 곳을 고릅니다. 같은 시각에 차가 몰리지 않도록 장소별 정원을 둡니다 |
| 대기 장소 안내 | 일찍 도착하면 주변 공영주차장의 요금·운영 시간·남은 자리를 보여 줍니다 |
| 아이와 만남 연결 | 아이에게 1회용 픽업 링크를 문자로 보내고, 아이는 웹에서 부모 차의 도착 예정 시각과 만날 장소를 실시간으로 봅니다. 아이의 위치는 수집하지 않습니다 |
| 정차·탑승·집까지 | 픽업 장소 정차, 탑승, 집까지 경로를 이어서 안내하고 픽업 기록을 남깁니다 |
| 차량 로그인 | 휴대폰으로 QR을 찍어 차량에 로그인하고(OAuth 2.0 Device Grant), 운전자가 바뀌면 계정을 전환합니다 |

```text
로그인 → 아이·시간표·집 등록 → 오늘 픽업 선택 → 픽업 장소 선택 → 출발 시각 확인
      → (필요하면) 대기 장소 → 아이에게 링크 전송 → 정차 → 탑승 → 집까지 → 완료
```

지원 지역(MVP): 서울 대치동 학원가, 안양 평촌 학원가

## 구성

```text
ride_mate/
├── mobility/   # 차량 앱 (Kotlin, Jetpack Compose, Pleos Connect SDK)
├── backend/    # API 서버 (.NET 10 ASP.NET Core, MySQL 8.4, n8n 수집 워크플로)
└── docs/api/   # 앱↔서버 API 명세 (OpenAPI)
```

아이용 웹(픽업 링크 화면)은 별도 저장소에서 Vercel로 배포합니다: https://ride-mate-child-web.vercel.app

| 영역 | 기술 |
|---|---|
| 차량 앱 | Kotlin, Jetpack Compose, Material 3, Pleos Vehicle 2.3.2 · NaviHelper 3.0.5 · FusedLocation 1.1.3, Kakao Map SDK 2.15.2 |
| 서버 | .NET 10 ASP.NET Core Web API, EF Core(쓰기) + Dapper(조회), MySQL 8.4(공간 인덱스), SSE |
| 데이터 수집 | n8n — 서울 TOPIS·경기 GITS 도로 소통(5분), 전국주차장정보표준데이터(매일) |
| 외부 API | 카카오모빌리티 길찾기, 카카오 로컬, 한국교통안전공단 주차 실시간, 솔라피 문자 |
| 배포 | Docker Compose, GitHub Actions(self-hosted runner) |

## 실행 방법

### 1. 차량 앱

1. Android Studio에서 `mobility/` 폴더를 엽니다.
2. `mobility/local.properties`를 만들고 아래 값을 넣습니다. 이 파일은 Git에 올리지 않습니다.

   ```properties
   sdk.dir=/path/to/Android/Sdk

   # 서버 주소 — 배포 서버를 쓰려면 아래 주소를 그대로 넣습니다. 비우면 Mock 데이터로 동작합니다
   RIDEMATE_API_BASE_URL=https://hsmac.tail94e53b.ts.net:10000

   # 카카오 지도 네이티브 앱 키 — 없으면 지도 자리에 안내 문구가 나옵니다
   KAKAO_NATIVE_APP_KEY=

   # 시연 옵션
   RIDEMATE_DEV_BUTTONS=false
   RIDEMATE_DEMO_PICKUP=false
   ```

3. Gradle Sync 뒤 Pleos Connect 에뮬레이터를 고르고 `app`을 실행합니다. 터미널에서는 `mobility/`에서 `./gradlew :app:assembleDebug`로 빌드합니다.

카카오 지도는 등록된 앱 서명에서만 표시되므로, 지도까지 확인하려면 아래 [APK](#apk)를 설치해 실행하세요.

자세한 설정과 문제 해결은 [`mobility/README.md`](mobility/README.md)를 참고하세요.

### 2. 서버

심사·테스트에는 **이미 배포된 서버**를 쓰면 됩니다. 따로 띄울 필요가 없습니다.

| 항목 | 주소 |
|---|---|
| API 서버 | https://hsmac.tail94e53b.ts.net:10000 |
| API 문서(Swagger) | https://hsmac.tail94e53b.ts.net:10000/swagger |
| 아이용 웹 | https://ride-mate-child-web.vercel.app |

## 테스트 환경 및 필요한 설정

### 테스트 환경

| 항목 | 값 |
|---|---|
| 차량 앱 | Android minSdk 28, targetSdk·compileSdk 37, Pleos Connect 에뮬레이터 |
| 서버 | 배포 서버(위 주소). MySQL 8.4, 데이터는 대치동·평촌 학원가 기준 |
| 아이용 웹 | 모바일 브라우저(문자로 받은 링크를 엽니다) |

### 서버에 미리 넣어 둔 데이터

| 데이터 | 대치동 | 평촌 |
|---|---|---|
| 학원가 경계 | 있음 | 있음 |
| 픽업 장소 | 4곳 | 5곳 |
| 어린이보호구역(전국어린이보호구역표준데이터) | 23곳 | 10곳 |
| 공영주차장(전국주차장정보표준데이터, 매일 갱신) | 23곳 | 22곳 |
| 실시간 도로 소통(5분 수집, 14~23시) | TOPIS 114개 구간 | GITS 184개 구간 |

### 로그인

- 차량 로그인은 휴대폰 인증번호로 받은 계정에 QR로 들어가는 방식입니다. 인증번호는 실제 문자로 발송됩니다.
- 따라서 앱을 끝까지 확인하려면 인증번호를 받고 QR을 찍을 휴대폰이 필요합니다.
- API만 확인할 때는 Swagger의 `POST /api/v1/Auth/M/DevToken`(개발 전용, 계정 ID로 토큰 발급)으로 받은 `access_token`을 Authorize에 넣으면 됩니다. 심사용 계정은 `9`이며, 요청 본문은 `{"userId": 9}`입니다.
- DevToken은 심사 편의를 위해 공모전 기간에만 열어 두며, 기간이 끝나면 닫습니다.

## Pleos Connect 에뮬레이터 버전과 화면 규격

| 항목 | 값 |
|---|---|
| 에뮬레이터 버전 | (작성 예정) |
| 화면 규격 | 2560 × 1440, 160dpi |

## APK

[RideMate APK 다운로드](./RideMate.apk)

다운로드한 APK를 Pleos Connect 에뮬레이터에 설치해 실행하세요.

## 팀 구성

| 역할 | 인원 |
|---|---|
| 기획·PM | 1 |
| UI/UX 디자인 | 2 |
| 차량 앱 개발 | 1 |
| 백엔드 개발 | 1 |
