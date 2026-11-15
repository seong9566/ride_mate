# RideMate (WAY:ME)

**RideMate**는 현대자동차 **Pleos Connect** 환경에서 학원 하원 픽업을 돕는 Android 차량용 앱입니다. 아이의 하원 일정에 맞춰 픽업 장소와 출발 시점을 안내하고, 대기·정차·탑승 과정을 지원합니다.

> 이 문서는 `mobility` 프로젝트의 코드와 설정을 기준으로 작성했습니다. 일부 기능은 Mock 데이터 또는 Pleos 지원 환경에서만 동작하며, 모든 기능의 실서버 연동이 완료되었다는 의미는 아닙니다.

## 1. 주요 기능

| 기능 | 설명 |
| --- | --- |
| **로그인 및 계정 관리** | QR 로그인, 휴대폰 OTP 인증, 차량 내 계정 선택, 인증 토큰 갱신 |
| **초기 설정** | 아이 정보, 학원 시간표, 집 주소 등록 |
| **홈 · 일정** | 오늘 픽업 일정과 주간 학원 시간표 확인 |
| **픽업 장소 추천** | 학원 주변 픽업 후보 장소 조회 및 선택 |
| **출발 안내** | 하원 시각, 차량 위치, 이동 시간을 바탕으로 추천 출발 시각 및 경로 표시 |
| **대기 장소 안내** | 출발 전 주변 주차장 추천, 이동 및 대기 상태 관리 |
| **아이와 만남 연결** | 아이에게 픽업 안내 링크 문자 발송, 열람·출발 상태 확인 |
| **정차 및 탑승** | 차량 정차 감지, 픽업 장소 도착 처리, 탑승 이벤트 기록 |
| **픽업 완료** | 집까지 이동 안내 및 픽업 기록 저장 |
| **음성 명령** | Gleo 액션 스키마 기반 차량 내 음성 연동 코드 |

**기본 이용 흐름**

```text
로그인 → 아이·시간표·집 등록 → 오늘 픽업 선택
      → 픽업 장소 선택 → 출발 시각 확인
      → (필요 시) 대기 장소 이동 → 픽업 장소 안내
      → 아이에게 링크 전송 → 정차 → 탑승 → 집 이동 → 완료
```

## 2. SDK 및 기술 목록

| 구분 | SDK / 기술 | 사용 목적 |
| --- | --- | --- |
| **차량 위치** | **Pleos FusedLocation SDK** | 차량 현재 위치 조회 및 픽업 경로의 출발 위치 설정 |
| **차량 내비게이션** | **Pleos NaviHelper SDK** | 내비게이션 연동 및 집 위치 등 차량 내비 정보 활용 |
| **차량 상태** | **Pleos Vehicle SDK** | 기어·주차 브레이크·도어 상태 등 차량 이벤트 연동 |
| **지도** | **Kakao Map Android SDK** | 지도, 픽업 지점 및 차량 위치 표시 |
| **음성** | **Gleo 연동 (`schema.json`)** | 차량 음성 액션 정의 및 호출 처리 |
| **QR 코드** | **ZXing Core** | 로그인 QR 코드 생성·표시 |
| **Android UI** | **Kotlin · Jetpack Compose · Material 3** | 차량용 앱 화면 구성 |
| **네트워크** | **프로젝트 내 `HttpCore`, `ApiClient`** | 백엔드 API 호출 및 응답·오류 처리 |
| **빌드** | **Gradle Kotlin DSL** | Android 앱 빌드 및 의존성 관리 |

Android 설정: `minSdk 28`, `targetSdk 37`, `compileSdk 37`. 라이브러리의 상세 버전은 `gradle/libs.versions.toml`에서 확인할 수 있습니다.

> Pleos SDK는 에뮬레이터·실차 환경 및 권한에 따라 지원 범위가 다를 수 있습니다. Gleo, NaviHelper, Vehicle SDK의 실제 동작 여부는 실행 환경에서 별도로 확인해야 합니다.

## 3. 실행 방법

### 3-1. 개발 환경 준비

1. **Android Studio**를 설치하고 프로젝트 최상위의 **`mobility/` 폴더**를 엽니다.
2. **Gradle Sync**를 실행합니다. Pleos 및 Kakao 관련 SDK 저장소에 접근할 수 있어야 합니다.
3. 실행 대상 Android 기기 또는 **Pleos 호환 에뮬레이터**를 준비합니다.
4. 프로젝트 루트의 **`local.properties`**에 필요한 설정을 입력합니다.

### 3-2. 로컬 설정

`mobility/local.properties` 예시:

```properties
sdk.dir=/path/to/Android/Sdk

# 카카오 지도 SDK
KAKAO_NATIVE_APP_KEY=YOUR_KAKAO_NATIVE_APP_KEY

# 서버 URL (비워 두면 Mock 저장소 사용)
RIDEMATE_API_BASE_URL=

# 개발 및 시연 옵션
RIDEMATE_DEV_BUTTONS=false
RIDEMATE_DEMO_PICKUP=false

# 디버그 전용 토큰 (필요한 경우)
RIDEMATE_DEV_ACCESS_TOKEN=

# Gleo 호출자 검증용 패키지 설정
GLEO_TRUSTED_PACKAGES=
```

- **카카오 지도**: 네이티브 앱 키 외에 앱 패키지명과 서명 키 해시 등록이 필요합니다. 키가 없으면 지도 자리표시가 나타날 수 있습니다.
- **Mock 실행**: `RIDEMATE_API_BASE_URL`을 비우면 Mock 저장소를 사용하는 구성입니다.
- **서버 연결**: 실제 서버를 사용할 때는 `RIDEMATE_API_BASE_URL`에 접근 가능한 서버 주소를 지정합니다.
- **시연 모드**: `RIDEMATE_DEMO_PICKUP=true`로 시연용 픽업 데이터를 사용할 수 있습니다.
- **보안**: API 키와 토큰이 포함된 `local.properties`는 Git에 커밋하지 않습니다.

### 3-3. 앱 실행

Android Studio에서 실행 기기를 선택하고 **`app` 실행 구성 → Run**을 누릅니다.

터미널에서 빌드하려면 프로젝트 루트에서 다음을 실행합니다.

```bash
./gradlew :app:assembleDebug
```

단위 테스트 실행:

```bash
./gradlew :app:testDebugUnitTest
```

> 이 README 수정 과정에서 빌드 및 테스트를 새로 실행하지는 않았습니다.

## 4. 주요 코드 위치

```text
mobility/
├── app/src/main/
│   ├── AndroidManifest.xml
│   ├── assets/schema.json                 # Gleo 액션 스키마
│   └── java/com/example/pickup_hyundai/
│       ├── MainActivity.kt                # 앱 진입점
│       ├── WayMeApplication.kt            # 지도 SDK 초기화
│       ├── ui/                             # 로그인·홈·픽업 화면
│       ├── platform/
│       │   ├── vehicle/                    # 차량 상태 SDK
│       │   ├── location/                   # 위치 SDK
│       │   ├── navi/                       # 내비게이션 SDK
│       │   └── gleo/                       # 음성 연동
│       ├── map/                            # 지도 UI
│       ├── data/remote/                    # 백엔드 API
│       ├── data/repository/                # Mock/Remote 저장소
│       └── core/net/                       # 네트워크 공통 처리
├── gradle/libs.versions.toml              # 의존성 버전
├── local.properties                       # 로컬 설정 (직접 구성)
└── gradlew                                # Gradle Wrapper
```

## 5. 실행 시 확인 사항

- **지도가 보이지 않음**: Kakao 키, 패키지명, 서명 키 해시를 확인합니다.
- **서버 데이터가 보이지 않음**: `RIDEMATE_API_BASE_URL` 및 Mock/Remote 저장소 선택 상태를 확인합니다.
- **차량 위치·기어·도어 정보가 동작하지 않음**: Pleos SDK 지원 기기·에뮬레이터와 권한을 확인합니다.
- **음성 명령이 동작하지 않음**: `assets/schema.json`, Gleo 연동 및 호출 패키지 설정을 확인합니다.

API별 요청·응답, 인증 및 오류 코드는 별도 **`API.md`**를 참고하세요(해당 파일을 프로젝트에 추가한 경우).
