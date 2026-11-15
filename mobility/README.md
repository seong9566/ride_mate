# RideMate (WAY:ME) — 차량용 픽업 지원 앱

현대자동차 **Pleos Connect** 환경을 대상으로 개발 중인 Android 차량용 애플리케이션입니다. 학원 하원 일정과 픽업 장소를 확인하고, 운전자의 이동·대기·정차·탑승 흐름을 지원합니다. 이 README는 첨부된 `mobility` 프로젝트의 **실제 코드와 설정**을 기준으로 작성했습니다. 일부 기능은 Mock/시연 모드 또는 플랫폼 연동 상태에 따라 동작이 달라집니다.

## 주요 기능

- **인증**: 차량 QR 로그인, 휴대폰 OTP 로그인, 계정 선택 및 토큰 갱신 관련 구현
- **초기 설정**: 아이 정보, 학원 시간표, 집 정보 입력 화면
- **홈**: 오늘 픽업 및 주간 일정 표시
- **픽업 흐름**: 픽업 장소 선택 → 출발 안내 → 대기 장소 추천 → 이동/정차 → 아이에게 링크 전송 → 탑승 → 기록
- **지도/차량 연동**: 카카오맵, Pleos FusedLocation, NaviHelper, Vehicle SDK 관련 코드
- **음성 연동**: Gleo 액션 처리와 `schema.json` 정의

> **구현 상태 주의:** 화면과 기능 코드가 존재한다는 사실이 모든 API가 실서버와 완전히 연동되어 있다는 뜻은 아닙니다. `Mock*Repository`, `Remote*Repository`, `RIDEMATE_DEMO_PICKUP` 등을 확인해 실행 모드를 구분하세요.

## 기술 스택

| 구분 | 사용 기술 |
| --- | --- |
| 언어 / UI | Kotlin, Jetpack Compose, Material 3 |
| 빌드 | Gradle Kotlin DSL, Android Gradle Plugin |
| Android 설정 | `minSdk 28`, `targetSdk 37`, `compileSdk 37` |
| 차량 SDK | Pleos Vehicle, NaviHelper, FusedLocation |
| 지도 | Kakao Map Android SDK |
| QR | ZXing Core |
| 네트워크 | 프로젝트 내 `HttpCore`, `ApiClient` 및 API 클래스 |

의존성의 정확한 버전은 `gradle/libs.versions.toml`을 참고하세요.

## 프로젝트 구조

```text
mobility/
├── app/
│   ├── build.gradle.kts
│   └── src/main/
│       ├── AndroidManifest.xml
│       ├── assets/schema.json         # Gleo 연동 스키마
│       └── java/com/example/pickup_hyundai/
│           ├── MainActivity.kt
│           ├── WayMeApplication.kt    # 카카오맵 SDK 초기화
│           ├── core/
│           │   ├── net/               # HTTP 클라이언트, API 응답/오류 처리
│           │   └── time/              # KST 시간 처리
│           ├── data/
│           │   ├── auth/              # 인증 세션 및 보안 저장
│           │   ├── remote/            # Auth, Child, Schedule, Pickup API
│           │   ├── repository/        # Remote/Mock 저장소
│           │   └── local/             # 로컬 설정 및 기록
│           ├── domain/                # 모델, 픽업 규칙, 경로/음성 로직
│           ├── platform/
│           │   ├── vehicle/           # 기어, 뒷문, 탑승 감지
│           │   ├── location/          # 차량 위치
│           │   ├── navi/              # 내비게이션
│           │   └── gleo/              # Gleo 액션 연동
│           ├── map/                   # 지도 컴포넌트 및 카메라 제어
│           ├── notify/                # 픽업 알림
│           └── ui/                    # auth, setup, home, pickup, navigation 등
├── disabled_voice/                   # 비활성화된 음성 코드
├── tools/                            # 개발 보조 스크립트
├── gradle/
├── settings.gradle.kts
└── gradlew
```

## 실행 방법

1. Android Studio에서 **`mobility/` 폴더**를 프로젝트로 엽니다.
2. Gradle Sync를 실행합니다. Pleos 및 카카오 SDK 저장소에 접근할 수 있어야 합니다.
3. 프로젝트 루트의 `local.properties`에 로컬 환경값을 설정합니다. **실제 API 키·토큰은 저장소에 커밋하지 마세요.**
4. 사용 가능한 Android 기기 또는 Pleos 호환 에뮬레이터를 선택하고 `app`을 실행합니다.

### 로컬 설정 예시

`mobility/local.properties`에 아래 키를 필요에 따라 설정합니다. 실제 값은 각자 발급·확인해야 합니다.

```properties
# Android SDK 경로는 개발 환경에 맞게 설정
sdk.dir=/path/to/Android/Sdk

# 카카오맵 네이티브 앱 키
KAKAO_NATIVE_APP_KEY=YOUR_KAKAO_NATIVE_APP_KEY

# 서버 연동 (비워 두면 Mock 저장소 사용)
RIDEMATE_API_BASE_URL=

# 개발/시연 옵션
RIDEMATE_DEV_BUTTONS=false
RIDEMATE_DEMO_PICKUP=false

# 디버그 전용 개발 토큰 (필요한 경우에만)
RIDEMATE_DEV_ACCESS_TOKEN=

# 공식 확인된 Gleo 호출 패키지만 지정
GLEO_TRUSTED_PACKAGES=
```

- `KAKAO_NATIVE_APP_KEY`가 비어 있으면 코드상 지도 자리표시가 사용됩니다. 카카오 지도 인증에는 앱 패키지명과 서명 키 해시 등록도 필요합니다.
- `RIDEMATE_API_BASE_URL`이 비어 있으면 Mock 저장소를 사용하는 설정입니다.
- `RIDEMATE_DEMO_PICKUP=true`는 서버 설정 여부와 별개로 차량에 등록한 시간표 기준의 시연 픽업 데이터를 사용하는 옵션입니다.
- `RIDEMATE_DEV_ACCESS_TOKEN`은 **debug 빌드에서만** 빌드 설정에 주입됩니다.
- 차량 위치/문/기어/내비 기능은 Pleos SDK 지원 환경과 권한에 의존합니다. 일부 권한은 플랫폼 승인 또는 런타임 허용이 필요합니다.

## 테스트

프로젝트 루트에서 다음 명령을 실행합니다.

```bash
./gradlew :app:testDebugUnitTest
./gradlew :app:assembleDebug
```

테스트 코드에는 아이 정보·전화번호 검증, 픽업 흐름, 정차 규칙, KST 포맷, 탑승 신호, 경로 계산, 음성 명령 파싱 관련 항목이 포함되어 있습니다. **이 README 작성 과정에서 빌드나 테스트를 직접 실행하지는 않았습니다.**

## API 및 구현 시 유의사항

- 차량 API는 주로 `Authorization: Bearer {access_token}`을 사용합니다.
- 일반 응답은 `{ "message": ..., "data": ..., "code": ... }` 형식이며, **HTTP 200이어도 `code`가 오류일 수 있습니다.**
- QR/OTP 토큰 발급은 `application/x-www-form-urlencoded` 방식이므로 JSON과 혼동하지 마세요.
- 픽업 이벤트는 `clientEventId`를 통한 중복 요청 방지와 KST 시각 처리에 주의해야 합니다.
- 서버 응답이 없거나 Mock으로 대체되는 화면을 실제 서버 연동 완료로 표시하지 마세요.
- 별도로 관리하는 **`API.md`**가 있다면 엔드포인트별 요청·응답·예외 처리의 기준 문서로 함께 확인하세요. 이 압축본 안에 `API.md`가 포함되어 있다고 가정하지 않습니다.

## 개발 시 확인할 사항

- Pleos 에뮬레이터/실차의 SDK 지원 범위와 권한 승인 여부
- 카카오맵 네이티브 키·패키지명·키 해시 등록
- Mock/Remote 저장소 전환 및 서버 Base URL
- 실제 차량 센서·내비 이벤트와 UI 상태 전환 연결
- Gleo 호출자 검증 및 음성 명령 시나리오
- 실서버 연동, 오류 처리, 화면별 E2E 테스트

---

**프로젝트명:** RideMate / WAY:ME  
**Android applicationId:** `com.example.pickup_hyundai`  
**기준:** 업로드된 `mobility.zip` 소스 구성
