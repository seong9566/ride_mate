# API 규약

## 라우트

`/api/{버전}/{도메인}/{클라이언트}/{인증구간}/{액션}` — 예: `/api/v1/Pickup/M/sign/GetPickupList`

| 세그먼트 | 값 |
|---|---|
| `{버전}` | `v1` |
| `{도메인}` | PascalCase 단수 (`Pickup`, `User`) |
| `{클라이언트}` | `M`(차량 앱), `W`(휴대폰 웹) |
| `{인증구간}` | 인증이 필요한 API는 `sign`. `M/sign`은 차량 JWT, `W/sign`은 웹 세션 쿠키를 쓴다. 자세한 인증 방식은 `security.md` |
| `{액션}` | PascalCase 동사+명사 |

| HTTP | 액션 접두 | 예 |
|---|---|---|
| POST | `Add` | `AddPickup` |
| GET(목록) | `Get` + `List` | `GetPickupList` |
| GET(상세) | `Detail` | `DetailPickup` |
| PATCH | `Update` | `UpdatePickup` |
| DELETE | `Delete` | `DeletePickup` |

- 수정은 PUT이 아니라 PATCH를 쓴다. 요청에서 `null`이거나 빠진 필드는 바꾸지 않는다(`architecture/dto.md`).

## 응답 `ResponseModel<T>`

500과 모델 검증 실패(400), 요청 빈도 제한(429, 본문 없음)을 뺀 응답 본문은 `{ message, data, code }` 형태다.

| code | 의미 | HTTP |
|---|---|---|
| 200 | 정상 처리 | 200 |
| 101·102·103·104·201·202 | 도메인별 부분 성공·분기 | 200 |
| 400 | 잘못된 요청(DTO null, 검증 실패) | 400 |
| 401 | 인증 실패·미인증 | 401 |
| 403 | 권한 없음 | 403 |
| 404 | 대상 없음 | 404 |
| 409 | 충돌(중복 등록 등) | 409 |
| 500 | 서버 오류 | 500 |

- 500 응답은 어디서 났든(Service의 `code = 500`, Controller가 잡은 예외) ASP.NET Core의 `ProblemDetails` 형태로 나간다. `ApiControllerBase.ToActionResult()`에 500 분기가 없어 `Problem(...)`으로 바뀌기 때문이다. [ADR-0005]
- `[ApiController]`의 모델 검증 실패(400)도 `ProblemDetails` 형태다. 앱은 두 형태를 모두 처리한다.
- 위 표에 없는 `code`는 500으로 떨어진다. 새 값을 쓰면 `ApiControllerBase`의 switch에 추가한다.
- `message`는 아래 문구만 쓴다. 앱이 문자열로 분기하므로 바꾸지 않는다.

| 상황 | 문구 |
|---|---|
| 정상 | `요청이 정상 처리되었습니다.` |
| 잘못된 요청·인증 실패 | `잘못된 요청입니다.` |
| 서버 오류(Service) | `서버에서 요청을 처리하지 못하였습니다.` (앱에는 전달되지 않는다) |
| 서버 오류(Controller) | `서버에서 요청을 처리하지 못하였습니다. 잠시후 다시 시도해주세요.` (500 응답의 `detail`에 들어간다) |

## `Token` 예외

토큰 발급 API는 OAuth 2.0 형식을 따르므로 위 규약에서 다음이 다르다. 다른 API는 영향이 없다.

- 주소는 `POST /api/v1/Auth/M/Token`이다. OAuth 토큰 발급이라 `Add` 접두를 쓰지 않는다. 이 API는 DB에 행을 하나 추가하는 것이 아니라 토큰을 발급·교체한다. 인증 구간이 없어(`sign` 아님) Bearer 없이 부른다.
- 요청 본문은 JSON이 아니라 `application/x-www-form-urlencoded`이고, 필드 이름은 OAuth 규약대로 snake_case다.
- 지원하는 `grant_type`은 `refresh_token`, `otp`, `urn:ietf:params:oauth:grant-type:device_code`다. `refresh_token`은 `refresh_token` 값을, `otp`는 `phone`·`code`·`device_id`를, `device_code` 방식은 `device_code`·`device_id`를 함께 보낸다.
- 성공(200)은 `{ message, data, code }`로 감싸지 않고 다음 본문을 그대로 돌려준다. `Cache-Control: no-store`가 붙는다.

```json
{
  "access_token": "…",
  "token_type": "Bearer",
  "expires_in": 3600,
  "refresh_token": "…",
  "session_id": 12,
  "setup_required": true
}
```

- `session_id`는 `LOGIN_SESSION_ID`다. `setup_required`는 그 계정의 운전자 이름이 비었거나 초기 설정을 완료하지 않았으면(`SETUP_DONE_DT` 없음) `true`다.
- `otp`로 같은 기기에서 같은 계정에 다시 로그인하면 그 계정·기기의 직전 차량 세션은 모두 끝나고 새 세션이 만들어진다.
- 실패(400)도 감싸지 않고 `{ "error": "...", "error_description": "..." }`를 돌려준다. `error_description`은 영어 한 줄이고 앱은 `error` 값으로 분기한다.

| `error` | 상황 |
|---|---|
| `invalid_request` | `grant_type`이나 그 방식에 필요한 값이 빠졌거나 형식이 틀렸다 |
| `unsupported_grant_type` | 지원하지 않는 `grant_type` |
| `invalid_grant` | refresh 토큰이 틀렸거나(128자보다 길어도 같다) 만료됐거나 로그아웃된 세션이거나 계정이 삭제됐다. `otp`에서는 대기 중인 로그인 인증번호가 없거나 인증 뒤 계정을 확인할 수 없다. `device_code`에서는 코드가 틀렸거나 이미 사용됐거나 발급 차량과 `device_id`가 다르거나 계정을 확인할 수 없다 |
| `otp_locked` | 인증번호 입력이 잠겼다. `retry_after`에 잠금이 풀리기까지 남은 초가 들어간다 |
| `otp_expired` | 인증번호가 만료됐다 |
| `otp_mismatch` | 인증번호가 틀렸다. `remaining_attempts`에 잠기기 전까지 남은 횟수가 들어간다 |
| `signup_required` | 인증번호는 맞지만 계정이 없다. 휴대폰 웹에서 가입해야 한다 |
| `authorization_pending` | QR 승인을 기다리는 중이다. `qr_status`에 `PENDING`·`SCANNED`·`VERIFIED` 중 현재 단계가 들어간다 |
| `slow_down` | 초 단위 서버 시각이 마지막 상태 조회 시각 + (`interval` - 1)초보다 이르다. 현재 `interval` 3에서는 2초 미만이다 |
| `expired_token` | 승인 전 QR의 3분 유효 시간이 지났거나, 승인 뒤 수령 유예 60초까지 지났다 |
| `access_denied` | QR 로그인이 취소됐다. `cancel_reason`에 `USER`·`DRIVING`·`PHONE` 중 취소 사유가 들어간다 |

- 서버 오류(500)는 다른 API와 같은 `ProblemDetails`다.
- `Token`은 요청 DTO에 검증 특성을 붙이지 않아 값이 빠져도 `[ApiController]`의 `ProblemDetails` 400이 아니라 `invalid_request`가 나간다(`architecture/dto.md`).

### QR 로그인 발급 `DeviceAuthorization`

- 주소는 `POST /api/v1/Auth/M/DeviceAuthorization`이고 인증 구간이 없다. 요청은 `application/x-www-form-urlencoded`이며 `device_id`를 보낸다.
- 이 API도 OAuth 2.0 기기 인증 형식이라 공통 응답으로 감싸지 않는다. 성공에는 `Cache-Control: no-store`가 붙고, 실패는 `Token`과 같은 `{ "error", "error_description" }` 형식이다.
- 성공 필드는 `device_code`(차량이 보관), `user_code`(QR 주소에 포함), `verification_uri`, `verification_uri_complete`, `expires_in`(180), `interval`(3), `match_code`(숫자 4자리)다.

```json
{
  "device_code": "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA",
  "user_code": "AAAAAAAAAAAAAAAAAAAAAA",
  "verification_uri": "http://localhost:3000/qr",
  "verification_uri_complete": "http://localhost:3000/qr?code=AAAAAAAAAAAAAAAAAAAAAA",
  "expires_in": 180,
  "interval": 3,
  "match_code": "0427"
}
```

## QR 로그인 취소

- 주소는 `DELETE /api/v1/Auth/M/DeleteQrLogin`이고 인증 구간이 없다. JSON 본문으로 `deviceCode`·`deviceId`·`reason`을 보낸다. `reason`은 `USER` 또는 `DRIVING`이다.
- 성공하거나 이미 취소됐으면 HTTP/code 200과 `data: true`, 요청을 찾을 수 없거나 기기가 다르면 404, 이미 `USED`면 409다.

## 인증번호 발송

- 주소는 `POST /api/v1/Auth/M/AddPhoneVerification`이다. 인증 구간이 없어(`sign` 아님) Bearer 없이 부른다.
- 요청 본문은 `application/json`이고 형식은 `{ "phone": "01012345678" }`다.
- 번호는 숫자로만 된 국내 휴대폰 번호여야 하며 형식은 `^01[016789][0-9]{7,8}$`다. 모델 검증에 실패하면 `ProblemDetails` 400이 나간다.
- 같은 번호에는 1분에 한 번만 보낸다. 아직 가입하지 않은 번호에도 보내며, 가입은 휴대폰 웹에서 진행한다.

| code | 상황 | data |
|---|---|---|
| 200 | 인증번호를 발송했다 | `expiresIn`은 180, `retryAfter`는 60 |
| 101 | 재발송 간격 1분이 지나지 않았다 | `expiresIn`은 0, `retryAfter`는 다시 보낼 수 있을 때까지 남은 초 |
| 102 | 인증번호 입력이 잠겨 있다 | `expiresIn`은 0, `retryAfter`는 잠금이 풀릴 때까지 남은 초 |
| 400 | 요청 값이 없거나 번호 형식이 틀렸다 | 모델 검증 실패는 `ProblemDetails` |

- 101·102는 HTTP 200이고 `message`는 `잘못된 요청입니다.`다.
- 처음 보는 번호로 동시에 여러 번 요청해도 한 번만 보낸다. 나머지는 101이다(교착이 나면 서버가 짧게 기다려 최대 두 번 다시 시도한다). 드물게 500이 나면 다시 요청하면 101이 온다.

## 휴대폰 웹 QR 로그인

휴대폰 웹은 QR 주소로 진입한 뒤 인증번호를 받고 확인한다. 처음 보는 번호면 `AddWebSession`이 code 101로 이름·필수 약관이 필요하다고 알리며, 같은 인증번호에 이름과 약관 동의를 붙여 다시 보낸다. 본인 확인 뒤 차와 휴대폰의 `matchCode`가 같은지 확인하고 `AddQrApproval`로 허용한다.

| API | 설명 |
|---|---|
| `GET /api/v1/Auth/W/DetailQrLogin` | QR 진입. 유효한 웹 세션이 있으면 곧바로 해당 계정을 QR에 연결한다 |
| `POST /api/v1/Auth/W/AddPhoneVerification` | 로그인 인증번호 발송. 차량용 발송 API와 요청·응답 규칙이 같다 |
| `POST /api/v1/Auth/W/AddWebSession` | 인증번호 확인, 처음 가입, 웹 세션 쿠키 발급, QR 본인 확인 완료 |
| `POST /api/v1/Auth/W/sign/AddQrApproval` | 숫자 확인 뒤 차량 로그인을 허용한다 |
| `DELETE /api/v1/Auth/W/sign/DeleteQrLogin` | 휴대폰에서 QR 로그인을 취소한다 |
| `DELETE /api/v1/Auth/W/sign/DeleteWebSession` | 현재 웹 세션을 끝내고, 진행 중인 QR을 다른 번호로 인증할 수 있게 되돌린다 |

`W/sign` API는 `__Host-rm_web_session` 쿠키가 필요하다. 쿠키 값은 응답 본문에 나오지 않는다. 이 구간의 404·409 응답은 인증된 요청이므로 `signedIn`이 `true`다.

| `qrState` | 의미 |
|---|---|
| `NOT_FOUND` | QR 요청을 찾을 수 없다 |
| `PENDING` | 아직 휴대폰에서 열지 않았다 |
| `SCANNED` | 휴대폰에서 열었지만 본인 확인 전이다 |
| `VERIFIED` | 휴대폰 본인 확인을 마치고 허용을 기다린다 |
| `APPROVED` | 휴대폰에서 차량 로그인을 허용했다 |
| `USED` | 이미 허용·사용됐거나 다른 휴대폰 계정이 잡은 QR이다 |
| `CANCELED` | 차 또는 휴대폰에서 취소했다. `cancelReason`은 `USER`·`DRIVING`·`PHONE`이다 |
| `EXPIRED` | QR의 3분 유효 시간이 지났다 |

| `error` | 의미 |
|---|---|
| `invalid_request` | 요청 값이나 이름 형식이 올바르지 않다 |
| `otp_not_requested` | 대기 중인 로그인 인증번호가 없거나 이미 사용했다 |
| `otp_locked` | 인증번호 입력이 잠겼다. `retryAfter`가 함께 온다 |
| `otp_expired` | 인증번호가 만료됐다 |
| `otp_mismatch` | 인증번호가 틀렸다. `remainingAttempts`가 함께 온다 |
| `terms_required` | 처음 가입하면서 필수 약관 둘 모두에 동의하지 않았다 |

처음 온 번호에서 이름이 비어 있으면 HTTP 200, code 101, `signupRequired: true`를 돌려준다. 인증번호는 사용 처리하지 않으므로 같은 `phone`·`code`·`userCode`에 공백을 지운 이름과 `agreeTerms: true`, `agreePrivacy: true`를 붙여 다시 요청한다.

- `DetailQrLogin`은 쿠키가 없거나 유효하지 않은 요청이 이미 `VERIFIED`인 QR을 열면 409 `USED`를 돌려준다. 같은 계정의 유효한 쿠키로 열 때만 `VERIFIED`를 그대로 보여준다.
- 휴대폰 취소는 현재 세션 계정으로 `VERIFIED`된, 만료 전 QR만 가능하다. `PENDING`·`SCANNED`는 현재 상태로 409, 다른 계정은 `USED`, 만료된 QR은 `EXPIRED`로 409다. 이미 `CANCELED`면 200이다.
- `DeleteWebSession`의 JSON 본문은 선택이다. 본문이나 `userCode`가 없으면 QR을 되돌리지 않고 현재 웹 세션만 끝낸다.

## 차 계정 목록

- 주소는 `GET /api/v1/Auth/M/GetLoginSessionList?deviceId=…`이다. 로그인 전에 부르므로 인증 구간이 없다.
- 그 기기의 살아 있는 `CAR` 세션 계정을 최근 사용 순으로 최대 10개 준다. 항목은 `loginSessionSeq`(로그인 응답의 `session_id`)와 `userName`뿐이다. 첫 항목이 가장 최근에 쓴 계정이다. 사용 시각·전화번호·계정 ID는 내보내지 않는다.
- 아래 커서 페이징의 예외다. 한 차에 계정이 몇 개뿐이고 PK가 아니라 마지막 사용 순으로 보여주기 때문이다.
- 같은 클라이언트(IPv4 주소, IPv6는 /64)에서 1분에 30번을 넘으면 본문 없이 429이고 `Retry-After`(초)가 붙는다(`security.md`).

## 출발 안내 계산

- 주소는 `GET /api/v1/Pickup/M/sign/DetailDepartureGuide`다. 인증 구간 `sign`이 있어 Bearer가 필요하고, 사용자는 JWT의 `sub`에서 꺼낸다.
- 요청 값은 query다(`currentDt`·`currentLat`·`currentLng`는 필수, `driveMin`·`pickupLat`·`pickupLng`는 선택). `pickupLat`와 `pickupLng`는 함께 보내야 하며 한쪽만 오면 `ProblemDetails` 400이 나간다.
- 계산 전용이라 DB에 쓰지 않는다. 대상 픽업은 아래 "오늘 픽업 계획"의 계획 API와 같은 규칙으로 고른다.
  1. 로그인한 계정에 오늘 진행 중 계획(`STATUS`가 `PLANNED`·`STARTED`·`WAITING`·`STOPPED`·`LOOPING`이고 `EXPIRE_DT`가 서버 현재 시각보다 뒤)이 있으면 그 계획의 픽업이다. 1시간 창·픽업 스위치·`SKIP` 예외와 상관없다. 여럿이면 `SLOT_DT`(없으면 하원 시각)가 가장 이른 것이다. 진행 중인지는 `currentDt`가 아니라 서버 현재 시각으로 본다.
  2. 없으면 `currentDt`의 날짜·요일로 고른다: 로그인한 계정의 살아 있는 아이 시간표 중 오늘 `PICKUP_YN = 1`이고 `LAST_PICKUP_DATE`가 오늘이 아닌 것에서 오늘 `SKIP` 예외를 빼고(`TIME_CHANGE`는 바뀐 하원 시각), 하원 시각 + 1시간이 `currentDt`보다 늦은 것 중 하원 시각이 가장 이른 하나다.
- 계획이 있으면 계획이 정답이다. 앱이 보낸 `pickupLat`·`pickupLng`는 무시하고(장소 바꾸기는 `UpdatePickupPlanLocation`) 카카오를 부르지 않는다. `pickup`은 계획의 장소(기본 지점이면 지점·저장된 도보 경로, 임의 좌표면 계획에 저장된 좌표·장소 이름(`spotName`)·도보 경로)이고 `walkSource`는 `STORED`, 도보 시간은 계획의 값이다. 추천 출발은 `slotDt − driveMin − 여유`이고 `childArrivalDt`는 지금처럼 하원 + 정리 + 도보다. 남은 여유(`slackMin`)는 계획이 있어도 `currentDt` 기준이다. 계획에 배정 장소가 없으면 `pickup`과 계산 필드가 null이고 `reasons`는 `NO_SPOT_AVAILABLE` 하나다.
- 계획이 있으면 학원 좌표가 없어도 `NO_ACADEMY_LOCATION`을 내지 않고 계획의 장소로 계산한다. 이때는 학원 앞 단속 근거(`ACADEMY_FRONT_RESTRICTED`) 판정만 빠진다.
- 응답에 `pickupPlanSeq`(계획 ID)·`slotDt`(계획의 배정 시각)·`planStatus`(계획 상태)가 있다. 계획이 없으면 셋 다 null이고, 계획에 배정 장소가 없으면 `slotDt`만 null이다. 계획의 배정 시각이 아이 도착 시각보다 늦으면(정원·늦은 출발로 미룸) `reasons`에 `SLOT_SHIFTED`가 들어가고 `shiftMin`에 미룬 분이 들어간다. 계획이 임의 좌표면 `CUSTOM_LOCATION`이다.
- 대기 장소 추천(`GET /api/v1/Pickup/M/sign/GetWaitingPlaceList`)은 같은 계산을 쓰므로 진행 중 계획이 있으면 계획의 장소와 배정 시각 기준의 `slackMin`·`reasons`(`SLOT_SHIFTED` 포함)를 쓴다. 한계: 주차장 운영 시간은 계획이 있어도 배정 시각이 아니라 하원 시각까지 운영하는지로 본다.

| code | 상황 | HTTP |
|---|---|---|
| 200 | 계산했다. 쓸 수 있는 기본 지점이 없으면 `pickup`과 계산 필드가 null이고 `reasons`에 `NO_SPOT_AVAILABLE`이 들어간다. 계획이 없을 때 학원 좌표가 없으면 `pickup`과 계산 필드가 null이고 `reasons`에 `NO_ACADEMY_LOCATION`이 들어간다(앱이 `pickupLat`·`pickupLng`를 보내도 같다) | 200 |
| 101 | 오늘 남은 픽업이 없다. `data`는 null이다 | 200 |
| 400 | 필수 값이 없거나 범위를 벗어났거나 픽업 좌표가 한쪽만 왔거나 형식이 틀린 값(해석할 수 없는 `currentDt`나 숫자)이나 `Z`·오프셋이 붙은 `currentDt`가 왔다 | 400 (`ProblemDetails`) |
| 401 | 인증 실패 | 401 |

- 101은 오류가 아닌 정상 상태라 `message`가 `요청이 정상 처리되었습니다.`다. 앱은 `code`로 나눈다.
- 한계: 하원 시각 + 1시간이 자정을 넘는 픽업(하원 23:30 등)은 00:00이 지나면 101이 나온다. 아이 도착 시각이 자정을 넘으면 요일 유형은 오늘 것으로 판정한다. `currentDt`는 오프셋 없는 KST 로컬 시각(`2026-11-11T21:14:00`)이어야 하며 `Z`·`+09:00`을 붙이면 400 `ProblemDetails`다(서버가 UTC로 바꿔 시각이 밀리기 때문).
- 필드·`reasons` 코드·계산식은 Notion "[차량] 출발 안내 계산" 페이지(https://app.notion.com/p/415aa638eaf8819eac85f85d0a5f1836)가 정본이다.

## 단속 구역 목록

- 주소는 `GET /api/v1/Pickup/M/sign/GetRestrictedZoneList`다. 인증 구간 `sign`이 있어 Bearer가 필요하고, 요청 query `version`은 선택이며 최대 64자다.
- 로그인한 부모의 살아 있는 아이 시간표에 있는 학원가를 요일별 픽업 스위치·오늘 예외와 무관하게 모두 준다. 학원가는 번호 오름차순이고, 단속 구역이 없는 학원가도 `zones: []`로 포함된다. 시간표가 없으면 HTTP/code 200과 `areas: []`이며 `version`도 준다. 이때도 같은 `version`을 보내면 101이다.
- 모든 단속 구역 유형을 준다. 점·선 구역은 서버가 `BUFFER_M` 반경을 적용해 면으로 바꾸며, `DetailDepartureGuide`의 단속 판정과 같은 데이터·같은 반경을 쓴다. 앱은 Pleos 내비 지도가 아니라 앱 창 안의 지도(카카오·네이버 등)에 면을 그리고 유형별 색을 정한다.
- 면 좌표는 소수 6자리다. `outer`와 `holes`의 첫 점은 끝에 반복하지 않고, 멀티 도형은 `polygons` 여러 개로 준다. 깨진 원본 데이터처럼 도형을 만들 수 없으면 해당 구역의 `polygons`는 빈 배열이다.
- `dayType`은 `WEEKDAY`·`SAT`·`HOLIDAY`이며 null이면 매일이다. 적용 시작·종료 시각은 `HH:mm`이고 null이면 종일이다. 현재 시각에 따른 필터·흐리기와 `HOLIDAY` 판정은 앱이 한다. 앱에 공휴일 데이터가 없으면 일요일만 `HOLIDAY`로 본다.

| code | 상황 | HTTP |
|---|---|---|
| 200 | 현재 전체 목록이다. 시간표가 없으면 `areas`가 빈 배열이다 | 200 |
| 101 | 요청한 `version`과 현재 버전이 같다. `data.version`만 주고 `areas`는 null이다 | 200 |
| 400 | `version`이 64자를 넘었다 | 400 (`ProblemDetails`) |
| 401 | 인증 실패 | 401 |

- 앱은 목록을 하루 한 번 받아 저장하고 다음 요청에 저장한 `version`을 보낸다. 101이면 저장한 데이터를 그대로 쓴다. `version`은 형식에 의존하지 않는 불투명한 문자열이며 학원가 구성·구역 데이터·학원가 이름·서버 도형 형식이 바뀌면 달라진다.
- 앱이 전체를 한 번에 캐시하는 목록이라 커서 페이징을 쓰지 않는다. `GetLoginSessionList`와 같은 예외다.
- 필드·구역 유형·도형 규칙은 Notion "[차량] 학원가 단속 구역 목록" 페이지(https://app.notion.com/p/415aa638eaf8817997e0f3e2bbefc22a)가 정본이다.

## 오늘 픽업 계획

차량이 경로 시작으로 오늘 픽업 계획(`PICKUP_PLAN`)을 만들어 시작하고, 픽업 장소를 바꾸고, 대기 장소 도착·정차·탑승을 기록하고, 부모가 오늘 픽업을 취소하는 여섯 API다. 모두 인증 구간 `sign`이 있어 Bearer가 필요하고, 사용자는 JWT의 `sub`에서 꺼낸다. 배정·잠금·만료·익명화 규칙은 `architecture/schema.md`의 `PICKUP_PLAN` 절이다.

- 계획은 경로 시작이 만든다. 앱은 출발 전에 출발 안내 계산(`DetailDepartureGuide`, 조회)만 부르고 계획을 미리 만들지 않는다. 출발 전 장소 바꾸기는 출발 안내의 `pickupLat`·`pickupLng` 미리보기로 하고, 출발 전 취소는 서버를 부르지 않는다. [ADR-0023]

| API | 설명 |
|---|---|
| `PATCH /api/v1/Pickup/M/sign/UpdatePickupPlanLocation` | 픽업 장소 바꾸기. 본문 `pickupPlanSeq`·`clientEventId`는 필수, `pickupLat`·`pickupLng`는 선택이다 |
| `PATCH /api/v1/Pickup/M/sign/UpdatePickupPlanStart` | 경로 시작. 오늘 계획이 없으면 만들면서 시작한다. 본문 `clientEventId`·`eventDt`·`driveMin`은 필수, `pickupLat`·`pickupLng`는 선택이다(둘 다 있거나 둘 다 없어야 하고, 계획이 없을 때만 쓴다. 없으면 기본 지점을 배정한다) |
| `PATCH /api/v1/Pickup/M/sign/UpdatePickupPlanWait` | 대기 장소 도착. 본문 `pickupPlanSeq`·`clientEventId`·`eventDt`·`parkingLotSeq`가 모두 필수다 |
| `PATCH /api/v1/Pickup/M/sign/UpdatePickupPlanStop` | 정차. 본문 `pickupPlanSeq`·`clientEventId`·`eventDt`·`lat`·`lng`가 모두 필수다. 서버가 배정 장소와의 거리와 정차 위치의 단속 구역을 판정한다 |
| `PATCH /api/v1/Pickup/M/sign/UpdatePickupPlanBoard` | 탑승. 본문 `pickupPlanSeq`·`clientEventId`·`eventDt`가 모두 필수다. 계획을 `BOARDED`로 끝낸다 |
| `DELETE /api/v1/Pickup/M/sign/DeletePickupPlan` | 부모 취소. JSON 본문 `pickupPlanSeq`·`clientEventId`가 모두 필수다(`DeleteQrLogin`과 같이 DELETE에 본문을 싣는다). 계획을 `CANCELED`로 끝낸다 |

- 여섯 API의 응답 `data`는 같은 모양이다: `{ reason, pickupPlanSeq, status, pickupDate, endTime, academyName, pickup, slotDt, expireDt, reassigned, stopCheck, cancelNotice, parkingLotSeq }`. `pickup`은 `{ locationType, pickupSpotSeq, name, lat, lng, walkMin, guideText, photoUrl }`이고 `locationType`은 `SPOT`(기본 지점)·`CUSTOM`(임의 좌표)이다. 배정 장소가 없으면 `pickup`과 `slotDt`가 null이다. 값이 없는 필드도 null로 나간다.
  - `stopCheck`는 `UpdatePickupPlanStop` 응답에만, `cancelNotice`는 `DeletePickupPlan` 응답에만 있고 다른 API는 null이다. `parkingLotSeq`는 계획 상태가 `WAITING`일 때만 대기 중인 주차장 ID이고 아니면 null이다(대기 뒤 출발하면 null. DB의 `PARKING_LOT_ID`는 남는다).
- `reassigned`는 이번 요청에서 다시 배정했는지다. 값이 실제로 바뀌었는지는 보지 않는다. `UpdatePickupPlanLocation`은 같은 좌표를 다시 보내도 true이고, `UpdatePickupPlanStart`는 진행 중 계획을 늦은 출발로 다시 배정했을 때만 true다. 계획을 새로 만든 경로 시작은 늘 false이고, 같은 `clientEventId`로 다시 부른 요청과 이미 시작한 계획의 경로 시작도 false다.
- 경로 시작의 대상 픽업은 "출발 안내 계산"과 같은 규칙으로 고르되 진행 중 계획 판정과 오늘 날짜·1시간 창 모두 서버 현재 시각 기준이다. 진행 중 계획이 있으면 그 계획을 시작하고 요청의 좌표는 무시한다(장소 바꾸기는 `UpdatePickupPlanLocation`).
- 시간 창: 계획이 없을 때 서버 현재 시각이 `하원 시각 − PickupPlan:CreateLeadHours`(3시간)보다 이르면 경로 시작이 409 `TOO_EARLY`다.
- 계획이 없을 때 학원 좌표나 학원가가 없으면 계획을 만들지 않고 경로 시작이 409 `NO_ACADEMY_LOCATION`이다. 이때 출발 안내는 지금처럼 `NO_ACADEMY_LOCATION` 근거로 동작한다.
- 임의 좌표는 위도 -90~90, 경도 -180~180 범위만 본다. 학원가 경계나 학원과의 거리는 보지 않는다. 서버는 학원 → 좌표 카카오 도보 경로(실패하면 직선 거리 ÷ 아이 도보 속도를 올린 분, 127분까지)와 카카오 좌표→주소 변환으로 도보 시간과 장소 이름을 정한다. 장소 이름은 도로명 주소의 건물 이름, 도로명 주소, 지번 주소 순으로 있는 것(100자까지)이고, 키가 없거나 실패·3초 시간 초과·결과 없음이면 `부모님이 정한 장소`다. 임의 좌표는 정원을 보지 않고 배정 시각이 `하원 + 정리 시간 + 도보 시간`이다.
- `UpdatePickupPlanLocation`에서는 좌표가 둘 다 없으면 "기본 지점으로 되돌리기"다. 위 라우트 절의 "PATCH에서 빠진 필드는 바꾸지 않음" 규칙의 예외다. 한쪽만 오면 400이다. 같은 좌표를 다시 보내도 다시 배정하고 이벤트를 남긴다. 계획이 `STARTED`면 늦은 출발 결과를 지키려고 배정 시각을 지금 `slotDt`보다 앞당기지 않는다. 바꿀 수 있는 상태는 `PLANNED`·`STARTED`·`WAITING`이다.
- `UpdatePickupPlanStart`는 `eventDt`를 시작 시각으로 기록한다. `eventDt`는 오프셋 없는 KST 로컬 시각이고 서버 현재 시각 −10분 ~ +1분 안이어야 한다. `driveMin`은 0~300이다.
  - 계획이 없으면 만들면서 시작한다. 차 도착 예상(`eventDt + driveMin`, 분 단위 올림)을 배정 시각의 하한으로 쓴다. 기본 지점은 우선순위·금지 시간·정원으로 배정하되 각 지점의 시작 시각이 아이 도착과 차 도착 예상 중 늦은 쪽이다(정원이 차면 다음 지점으로 넘어간다). 임의 좌표는 아이 도착과 차 도착 예상 중 늦은 쪽이 배정 시각이다. 계획을 `STARTED`로 넣고 `PLANNED`·`STARTED` 이벤트를 남긴다. [ADR-0023]
  - 계획을 만드는 사이 다른 요청이 같은 픽업의 계획을 만들었으면(빠르게 두 번 누름) 그 계획을 시작한다. 그래서 두 요청 모두 같은 계획으로 200이다.
  - 진행 중 계획이 있으면 `PLANNED`·`WAITING`을 `STARTED`로 바꾼다(대기 뒤 출발).
  - 늦은 출발(진행 중 계획을 시작할 때): 차 도착 예상(`eventDt + driveMin`, 분 단위 올림)이 `slotDt`보다 늦으면 같은 장소에서 다시 배정하고 `reassigned`가 true다. 임의 좌표는 차 도착 예상 시각이 배정 시각이 된다. 기본 지점은 차 도착 예상부터 `PickupPlan:SlotShiftMaxMin`(2분)까지 1분씩 늦춰 정원이 남은 첫 시각을 쓰고, 자리가 없으면 정원을 넘어도 차 도착 예상 시각으로 배정한다. 다른 지점으로 옮기지 않는다. [ADR-0021] 배정 장소가 없는 계획은 다시 배정하지 않는다.
  - 이미 `STARTED`면 `clientEventId`와 상관없이 지금 계획을 200으로 돌려준다. 단, `eventDt` 범위 검사가 먼저라 10분이 지난 재시도는 400이다.
- `clientEventId`는 차량이 요청마다 만드는 UUID다. 같은 요청을 다시 부를 때는 같은 값을 보낸다. 같은 번호의 같은 유형 이벤트(장소 바꾸기는 `REASSIGNED`, 경로 시작은 `STARTED`)가 이미 있으면 다시 반영하지 않고 지금 계획을 200으로 돌려준다. 이 계획의 다른 어떤 이벤트(링크 발송·다시 알림 포함)에 쓰인 번호면 `data`가 null인 400이다.
- `expireDt`는 `slotDt`(없으면 하원 시각) + `PickupPlan:ExpireAfterHours`(1시간)이고 재배정하면 다시 계산한다. `PickupPlan:ExpireAfterHours`가 `DepartureGuide:PickupWindowHours`보다 작으면 서버가 기동하지 않는다(창이 남은 픽업의 계획이 먼저 만료되지 않게). 서버 주기 작업이 `PickupPlan:ExpireIntervalSec`(60초)마다, 그리고 서버 시작 직후 한 번 `expireDt`가 지난 진행 중 계획을 `EXPIRED`로 끝내고 익명화한다. 끝난 계획은 `UpdatePickupPlanLocation`에서 404이고, 그 시간표는 그날 다시 대상이 되지 않는다(`LAST_PICKUP_DATE`. 경로 시작은 다른 픽업이 없으면 101).

| code | `UpdatePickupPlanLocation` | `UpdatePickupPlanStart` |
|---|---|---|
| 200 | 바꿨다(같은 `clientEventId` 재요청 포함) | 시작했다(계획을 만들었거나, 이미 시작했거나, 같은 `clientEventId` 재요청 포함) |
| 101 | — | 계획이 없고 오늘 남은 픽업도 없거나, 잠근 뒤 다시 보니 대상이 아니다(스위치 꺼짐·삭제·그날 `SKIP` 등). `data`는 null |
| 400 | 필수 값 없음·범위 밖은 `ProblemDetails`. 좌표가 한쪽만 왔거나 `clientEventId`가 다른 이벤트에 쓰였으면 `data` null | 필수 값 없음·`driveMin`·위도·경도 범위 밖은 `ProblemDetails`. 좌표가 한쪽만 왔거나 `eventDt`가 범위 밖이거나 `clientEventId`가 다른 이벤트에 쓰였으면 `data` null |
| 401 | 인증 실패 | 인증 실패 |
| 404 | 계획이 없거나, 로그인한 계정의 계획이 아니거나, 끝났다(`BOARDED`·`EXPIRED`·`CANCELED`). `data`는 null | 고른 진행 중 계획이 잠그는 사이 끝났다. `data`는 null |
| 409 | `ALREADY_STOPPED`, `PLAN_CLOSED`, `NO_ACADEMY_LOCATION` | `TOO_EARLY`, `NO_ACADEMY_LOCATION`(계획이 없을 때), `ALREADY_STOPPED`, `PLAN_CLOSED`(진행 중 계획일 때) |

정차·탑승·취소·대기 장소 도착의 code는 아래 "상태 바꾸기"에 따로 적었다.

- 200·101의 `message`는 `요청이 정상 처리되었습니다.`, 400(`data` null)·404·409는 `잘못된 요청입니다.`다. 101은 오류가 아닌 정상 상태다. 앱은 `code`와 `data.reason`으로 나눈다.
- 409의 `data`는 `reason`만 채운 같은 모양이다(나머지 필드는 null, `reassigned`는 false). 링크 발송 API와 같은 방식이다.

| `reason` (409) | 상황 | API |
|---|---|---|
| `TOO_EARLY` | 계획이 없고 서버 현재 시각이 하원 시각 − 3시간보다 이르다 | `UpdatePickupPlanStart` |
| `NO_ACADEMY_LOCATION` | `UpdatePickupPlanStart`: 계획이 없고 학원 좌표나 학원가가 없다. `UpdatePickupPlanLocation`: 좌표를 보냈는데 계획 학원에 좌표가 없다 | `UpdatePickupPlanStart`, `UpdatePickupPlanLocation` |
| `ALREADY_STOPPED` | 계획이 정차(`STOPPED`)나 한 바퀴(`LOOPING`) 중이다 | `UpdatePickupPlanLocation`, `UpdatePickupPlanStart`, `UpdatePickupPlanWait` |
| `PLAN_CLOSED` | `EXPIRE_DT`가 지났는데 만료 처리가 아직 돌지 않았다 | 여섯 API 모두 |
| `INVALID_STATUS` | 계획이 한 바퀴(`LOOPING`) 중이다. 한 바퀴 뒤 정차는 아직 받지 않는다 | `UpdatePickupPlanStop` |
| `INVALID_PARKING_LOT` | 주차장이 없거나, 삭제됐거나, 계획과 다른 학원가의 주차장이다 | `UpdatePickupPlanWait` |

- 경로 시작의 첫 판정: 입력 검증 400(필수 값·범위·좌표 한쪽만·`eventDt` 범위) → 대상 고르기. 대상이 없으면 101이다. 진행 중 계획이 있으면 아래 순서를 그 계획으로 탄다. 없으면 `TOO_EARLY` → `NO_ACADEMY_LOCATION`을 보고, `AREA`·`TIMETABLE_DAY`를 잠근 뒤 101·`NO_ACADEMY_LOCATION`·`TOO_EARLY`를 다시 보고 만든다.
- 판정 순서: `UpdatePickupPlanLocation`과 진행 중 계획의 `UpdatePickupPlanStart`는 입력 검증 400 → 404(없음·남의 계획) → `ALREADY_STOPPED` → 404(끝난 계획) → `PLAN_CLOSED` 순이다. 그다음 `UpdatePickupPlanLocation`은 좌표를 보냈을 때 `NO_ACADEMY_LOCATION`을 보고(잠그기 전에 한 번만 본다), `UpdatePickupPlanStart`는 "이미 시작함 → 200"이다. 잠근 뒤에는 404·`ALREADY_STOPPED`·404·`PLAN_CLOSED`를 다시 보고(`UpdatePickupPlanStart`는 "이미 시작함 → 200"도), 그다음 `clientEventId`를 확인한다: 같은 유형 이벤트면 200, 이 계획의 다른 이벤트에 쓰였으면 400(`data` null).
- 필드·예시는 Notion "[차량] 픽업 장소 바꾸기"(https://app.notion.com/p/416aa638eaf881b38b78ca310e6de212), "[차량] 경로 시작"(https://app.notion.com/p/416aa638eaf881819262e44ed3ec9ff1), "[차량] 대기 장소 도착"(https://app.notion.com/p/416aa638eaf8815eaa1cfa03fa0029f3), "[차량] 정차 기록"(https://app.notion.com/p/416aa638eaf881bd963bfd868d4396ea), "[차량] 탑승 기록"(https://app.notion.com/p/416aa638eaf881d584e3c1255b50895a), "[차량] 오늘 픽업 취소"(https://app.notion.com/p/416aa638eaf881b0a1d1d96704739225) 페이지가 정본이다.

### 상태 바꾸기 — 대기 장소 도착·정차·탑승·취소

`UpdatePickupPlanWait`·`UpdatePickupPlanStop`·`UpdatePickupPlanBoard`·`DeletePickupPlan`은 경로 시작과 같은 골격이다: 401 → 입력 검증 400 → 잠금 없이 판정 → 계획 행을 PK로 잠그는 트랜잭션 → 잠근 값으로 다시 판정 → 쓰기. 재배정을 하지 않아 `AREA`는 잠그지 않는다. 잠금 순서와 끝내기·익명화는 `architecture/schema.md`의 `PICKUP_PLAN` 절이다.

- `eventDt`(취소에는 없다)는 오프셋 없는 KST 로컬 시각이고 서버 현재 시각 −10분 ~ +1분 안이어야 한다. 밖이면 `data`가 null인 400이다. 이 검사가 맨 앞이라 10분이 지난 재전송도 400이다(`UpdatePickupPlanStart`와 같다).
- 받는 상태:

| API | 받는 상태 | 바꾸는 것 |
|---|---|---|
| `UpdatePickupPlanWait` | `PLANNED`·`STARTED`·`WAITING`. `STOPPED`·`LOOPING`은 409 `ALREADY_STOPPED` | `STATUS = WAITING`, `PARKING_LOT_ID`, `WAIT_START_DT`(비어 있을 때만 `eventDt`), `WAIT_STARTED` 이벤트. 이미 `WAITING`이면 주차장만 바꾸고 `WAIT_START_DT`는 그대로다 |
| `UpdatePickupPlanStop` | `PLANNED`·`STARTED`·`WAITING`·`STOPPED`. `LOOPING`은 409 `INVALID_STATUS` | `STATUS = STOPPED`, `STOP_DT`(비어 있을 때만 `eventDt`), `STOPPED` 또는 `WRONG_STOP` 이벤트 |
| `UpdatePickupPlanBoard` | 진행 중인 모든 상태 | `BOARD_DT = eventDt`, 계획을 `BOARDED`로 끝낸다(익명화, `LAST_PICKUP_DATE`, 링크 종료, `BOARDED` 이벤트) |
| `DeletePickupPlan` | 진행 중인 모든 상태 | 계획을 `CANCELED`로 끝낸다(익명화, 링크 종료, `CANCELED` 이벤트). `LAST_PICKUP_DATE`는 바꾸지 않아 그날 경로 시작으로 다시 만들 수 있다 |

- 취소는 `LAST_PICKUP_DATE`를 바꾸지 않으므로 같은 픽업이 곧바로 다시 계획 대상이 된다. 부모가 다시 경로 시작을 누르면 계획을 다시 만든다.

- `PLANNED`·`WAITING`에서 바로 정차해도 받는다(경로 시작 이벤트가 늦게 오거나 내비를 앱 밖에서 시작한 경우). 탑승은 정차가 빠지거나 늦게 와도 기록한다.
- 대기 장소 도착의 주차장(`parkingLotSeq`, 대기 장소 추천의 주차장 ID)은 있고, 삭제되지 않았고, 계획과 같은 학원가여야 한다. 아니면 409 `INVALID_PARKING_LOT`이다. 잠그기 전에 한 번만 본다. 도착 판정은 차량 앱이 한다.
- 대기 뒤 출발은 `UpdatePickupPlanStart`다(`WAITING`을 받는다). 출발하면 `START_DT`를 새 출발 시각으로 덮어쓰고 늦은 출발 재배정을 한다.

정차 판정(`UpdatePickupPlanStop`):

- 배정 장소는 기본 지점이면 지점 좌표, 임의 좌표면 그 좌표다. `distanceM`은 정차 위치와 배정 장소의 직선 거리(m, 반올림)이고 `atSpot`은 `distanceM`이 `PickupPlan:StopRadiusM`(30m) 이하인지다. 장소는 잠근 뒤 다시 읽어 계산한다(잠금을 기다리는 사이 장소 바꾸기가 커밋했을 수 있다).
- 단속 판정은 잠그기 전에 출발 안내와 같은 규칙으로 한다(요일 유형·공휴일·구역 활성 시간, 판정 시각은 `eventDt`).
- 배정 장소가 있고 반경 밖이면 `WRONG_STOP` 이벤트, 그 밖(반경 안, 배정 장소가 없는 계획)은 `STOPPED` 이벤트다. 어느 쪽이든 상태는 `STOPPED`라 다시 알림(`AddPickupRemind`)을 쓸 수 있다. 경고를 받고 차를 옮겨 다시 서면 새 `clientEventId`로 다시 부른다. 이벤트가 하나 더 생기고 `STOP_DT`는 그대로다.
- 정차 좌표와 거리는 저장하지 않는다(`security.md`).

| `stopCheck` 필드 | 타입 | 설명 |
|---|---|---|
| `atSpot` | boolean | 배정 장소 반경 안에 섰는지. 배정 장소가 없는 계획은 false |
| `distanceM` | integer | 배정 장소까지 거리(m). 배정 장소가 없거나 같은 요청을 다시 보낸 응답이면 null |
| `enforcement` | string | 정차 위치의 단속 판정 `RESTRICTED`·`CLEAR`·`UNKNOWN`. 출발 안내의 `enforcement.status`와 같은 규칙이다: 구역에 걸리면 `RESTRICTED`, 판정할 수 없으면(학원가 밖·구역 데이터 없음) `UNKNOWN`, 걸리지 않아도 그해 공휴일이 적재되지 않았으면 `UNKNOWN`, 아니면 `CLEAR` |
| `zoneTypes` | array of strings | 걸린 구역 유형. `RESTRICTED`가 아니면 빈 배열 |

- 앱은 `atSpot`이 false이고 `enforcement`가 `RESTRICTED`일 때 경고 카드(V09)를 띄운다. 문구 조합은 앱이 한다.

부모 취소와 취소 문자(`DeletePickupPlan`):

- 아이가 링크를 받았을 수 있으면 취소 문자를 보낸다. 이 계획의 `LINK_SENT` 이벤트 중 하나라도 `success`가 true이거나, null(진행 중)이거나, false라도 결과 코드가 `TIMEOUT`·`ERROR`(도착했는지 모름)이면 대상이다. 사업자가 거절한 것이 확실한 실패만 있거나 링크 발송이 없으면 대상이 아니다. 받지 못했을 수 있는 아이에게 문자가 가는 것보다 받은 아이가 오지 않을 차를 기다리는 쪽이 더 위험하다고 본다.
  - 결과 없이 `PickupLink:InFlightTimeoutSec`(30초)가 지난 진행 중 `LINK_SENT`도 여기서는 "받았을 수 있음"으로 센다. 전달 상태 조회(`DetailPickupLinkStatus`)와 발송 횟수 판정은 같은 이벤트를 실패로 센다.
  - 대상이어도 계정이나 아이가 삭제됐거나, 아이 번호(`CHILD.PHONE_ENC`)가 없거나 복호화할 수 없으면 보내지 않는다.
- 번호와 부모 이름은 익명화 전에 같은 트랜잭션에서 읽어 두고, 커밋한 뒤 잠금 밖에서 SMS로 보낸다(아이 정보의 `SEND_CHANNEL`과 상관없다). 본문은 `[라이드메이트] 오늘 {부모 이름} 차 픽업은 취소됐어. 부모님께 연락해 줘`이고 링크는 넣지 않는다.
- 결과는 응답의 `cancelNotice`로만 알린다. `NONE`(대상 아님), `SENT`(사업자 접수), `FAILED`(사업자 거절·시간 초과·발송 중 예외)다. 이벤트는 남기지 않고 실패해도 다시 보내지 않는다. 취소는 이미 커밋됐으므로 발송이 실패해도 200이다.
- `CANCELED` 이벤트의 `EVENT_DT`는 서버 시각(잠근 뒤)이다.

끝난 응답의 모양:

- 탑승·취소 응답은 `pickupPlanSeq`, `status`(`BOARDED`·`CANCELED`), `reassigned: false`(취소는 `cancelNotice`도)만 채우고 나머지는 null이다. 끝난 계획은 개인정보 칸이 비어 있다.
- 진행 중 계획의 대기 장소 도착·정차 응답은 다른 계획 API와 같이 지금 계획 전체다(정차는 `stopCheck`를 더한다). 이미 끝난 계획에 같은 요청을 다시 보낸 경우(아래 재요청)는 대기·정차도 최소 모양이다.

같은 요청을 다시 보낸 경우(재요청):

- 같은 계획에 같은 `clientEventId`의 이벤트가 이미 있고 그 유형이 이 API의 유형(`UpdatePickupPlanWait`: `WAIT_STARTED`, `UpdatePickupPlanStop`: `STOPPED`·`WRONG_STOP`, `UpdatePickupPlanBoard`: `BOARDED`, `DeletePickupPlan`: `CANCELED`)이면 다시 반영하지 않고 200이다. 다른 유형의 이벤트에 쓰인 번호면 `data`가 null인 400이다.
- 재요청 확인은 409 판정보다 먼저다. `EXPIRE_DT`가 지났지만 만료 처리가 아직 돌지 않은 사이에 온 재요청도 200이다. 잠근 뒤에도 같은 순서다.
- 계획이 이미 끝났어도(탑승·취소·만료, `USER_ID`가 NULL) 위 조건에 맞으면 200이다. 응답은 `pickupPlanSeq`, `status`(계획의 지금 끝난 상태), `reassigned: false`만 채운 최소 모양이고, 정차면 `stopCheck`, 취소면 `cancelNotice: "NONE"`을 더한다. 그 밖의 끝난 계획은 404다. `clientEventId`는 차량이 만든 UUID라 남이 맞힐 수 없다고 본다.
- 진행 중 계획의 재요청 응답은 대기 장소 도착·정차면 지금 계획 전체(정차는 `stopCheck`를 더한다)다. 탑승·취소의 재요청은 계획이 이미 끝났으므로 위 최소 모양이다.
- 정차 재요청의 `stopCheck`는 처음 이벤트의 유형과 `DETAIL`로 다시 만든다. `distanceM`은 null이다. `STOPPED`는 `atSpot: true`, `enforcement: "UNKNOWN"`, `zoneTypes: []`이고, `WRONG_STOP`은 `atSpot: false`에 `DETAIL`의 구역 유형이 있으면 `RESTRICTED`, 없으면 `UNKNOWN`이다. 한계: 배정 장소가 없는 계획의 `STOPPED`도 재요청에서는 `atSpot: true`로 나온다.
- 취소 재요청은 문자를 다시 보내지 않고 `cancelNotice`가 `NONE`이다.

| code | `UpdatePickupPlanWait` | `UpdatePickupPlanStop` | `UpdatePickupPlanBoard` | `DeletePickupPlan` |
|---|---|---|---|---|
| 200 | 기록했다(재요청 포함) | 기록했다(재요청 포함) | 끝냈다(재요청 포함) | 취소했다(재요청 포함) |
| 400 | 필수 값 없음·`parkingLotSeq` 1 미만은 `ProblemDetails`. `eventDt` 범위 밖·다른 유형 `clientEventId`는 `data` null | 필수 값 없음·위도·경도 범위 밖은 `ProblemDetails`. `eventDt` 범위 밖·다른 유형 `clientEventId`는 `data` null | 필수 값 없음은 `ProblemDetails`. `eventDt` 범위 밖·다른 유형 `clientEventId`는 `data` null | 필수 값 없음은 `ProblemDetails`. 다른 유형 `clientEventId`는 `data` null |
| 401 | 인증 실패 | 인증 실패 | 인증 실패 | 인증 실패 |
| 404 | 계획이 없거나, 로그인한 계정의 계획이 아니거나, 끝났다(재요청 제외). `data`는 null | 같음 | 같음 | 같음 |
| 409 | `ALREADY_STOPPED`, `PLAN_CLOSED`, `INVALID_PARKING_LOT` | `PLAN_CLOSED`, `INVALID_STATUS` | `PLAN_CLOSED` | `PLAN_CLOSED` |

- `message`는 200이 `요청이 정상 처리되었습니다.`, 400(`data` null)·404·409가 `잘못된 요청입니다.`다. 409의 `data`는 `reason`만 채운 같은 모양이다.
- 판정 순서(잠그기 전): 입력 검증 400 → 계획이 없거나 끝났으면 재요청 200, 아니면 404 → 남의 계획 404 → 재요청 200 → API별 409 → (`UpdatePickupPlanWait`만) `INVALID_PARKING_LOT`. API별 409는 `UpdatePickupPlanWait`가 `ALREADY_STOPPED` → `PLAN_CLOSED`, `UpdatePickupPlanStop`이 `PLAN_CLOSED` → `INVALID_STATUS`, 탑승·취소가 `PLAN_CLOSED`다. 잠근 뒤에는 끝났으면 재요청 200 또는 404 → 남의 계획 404 → 재요청 200 → API별 409 → 다른 유형 `clientEventId` 400 순이다. 그래서 다른 유형 이벤트에 쓰인 번호라도 409에 먼저 걸리면 409다.

## 픽업 링크 문자 발송

아이 휴대폰 번호로 픽업 링크 문자를 보내고 전달 상태를 읽는 세 API다. 모두 인증 구간 `sign`이 있어 Bearer가 필요하고, 사용자는 JWT의 `sub`에서 꺼낸다. 번호는 앱이 보내지 않고 서버가 DB의 아이 정보에서 읽는다.

| API | 설명 |
|---|---|
| `POST /api/v1/Pickup/M/sign/AddPickupLink` | 링크 문자 발송. 최초 발송과 실패 뒤 다시 보내기가 같은 API다. 본문 `pickupPlanSeq`·`etaDt`·`clientEventId`가 모두 필수다 |
| `POST /api/v1/Pickup/M/sign/AddPickupRemind` | 정차 뒤 "도착했어요" 다시 알림. 정차 기록(`UpdatePickupPlanStop`, 잘못된 위치 정차 포함) 뒤 사용할 수 있다. 본문 `pickupPlanSeq`·`clientEventId`가 모두 필수다 |
| `GET /api/v1/Pickup/M/sign/DetailPickupLinkStatus` | 전달 상태 조회. query `pickupPlanSeq`가 필수다. 차량이 10초 간격으로 부른다 |

- `etaDt`는 차량 내비가 계산한 도착 예정 시각이고 오프셋 없는 로컬(KST) 시각이다(`2026-11-11T22:04:00`). 문자의 `HH:mm`에 쓰이며 서버 현재 시각 기준 -10분 ~ +3시간 안이어야 한다.
- `clientEventId`는 차량이 요청마다 만드는 UUID다. 같은 발송을 다시 부를 때는 같은 값을, 새로 보낼 때는 새 값을 쓴다.
- 응답 `data`는 `{ reason, sent, remainingAttempts, receiverLast4 }`이고 값이 없는 필드도 null로 나간다. `sent`는 이번 시도를 사업자가 접수했는지, `remainingAttempts`는 남은 시도 횟수, `receiverLast4`는 아이 번호 뒤 4자리다.

두 발송 API(`AddPickupLink`·`AddPickupRemind`)의 응답이다.

| code | 상황 | HTTP |
|---|---|---|
| 200 | 문자 사업자가 접수했다. `sent`는 true | 200 |
| 101 | 사업자가 거절했거나 5초 안에 응답하지 않았다. `sent`는 false이고 `remainingAttempts`가 남아 있으면 다시 보낼 수 있다 | 200 |
| 400 | 값이 없거나 형식이 틀렸다(`ProblemDetails`). `etaDt`가 범위 밖이거나 `clientEventId`가 이 계획의 다른 어떤 이벤트(다른 유형의 발송, 경로 시작·재배정 등)에 쓰였으면 `data`가 null인 `{ message, data, code }`다 | 400 |
| 401 | 인증 실패 | 401 |
| 404 | 계획이 없거나, 로그인한 계정의 계획이 아니거나, 탑승·만료·취소로 끝났다. 끝난 계획은 `USER_ID`가 NULL이라 남의 계획과 구분되지 않는다 | 404 |
| 409 | 보낼 수 없는 상태. `data.reason`에 사유가 들어간다 | 409 |

- 101은 오류가 아닌 정상 분기라 `message`가 `요청이 정상 처리되었습니다.`다. 400(`etaDt`·`clientEventId` 경우)·404·409는 `잘못된 요청입니다.`다. 앱은 `code`와 `data.reason`으로 나눈다.

| `reason` (409) | 상황 | API |
|---|---|---|
| `PLAN_CLOSED` | `EXPIRE_DT`가 지났는데 만료 처리가 아직 돌지 않았다. 이미 `BOARDED`·`EXPIRED`·`CANCELED`로 끝난 계획은 `USER_ID`가 NULL이라 404가 먼저 나간다 | 둘 다 |
| `NO_SPOT` | 계획에 기본 지점도 임의 좌표도 없다(배정된 지점이 그사이 삭제된 경우 포함) | 둘 다 |
| `NO_CHILD_PHONE` | 아이가 삭제됐거나 `PHONE_ENC`가 비었거나 복호화할 수 없다 | 둘 다 |
| `NOT_STOPPED` | `STATUS`가 `STOPPED`·`LOOPING`이 아니다. `UpdatePickupPlanStop`이 `STOPPED`로 바꾼다 | `AddPickupRemind` |
| `LINK_NOT_SENT` | 접수된 링크 발송(`LINK_SENT` 성공)이 없다. 링크 세션이 없는 경우도 같다 | `AddPickupRemind` |
| `ALREADY_SENT` | 그 유형의 발송이 이미 접수됐다 | 둘 다 |
| `IN_PROGRESS` | 그 유형의 발송이 진행 중이다(결과가 없고 만든 지 `PickupLink:InFlightTimeoutSec`, 30초가 안 됐다) | 둘 다 |
| `LIMIT_REACHED` | 그 유형의 시도가 `PickupLink:MaxAttempts`(3)에 이르렀다. 성공·실패·진행 중을 모두 센다(성공 1회 + 실패 재시도 2회) | 둘 다 |

- 판정 순서는 404 → `PLAN_CLOSED` → `NO_SPOT` → `NO_CHILD_PHONE` → (`AddPickupRemind`만) `NOT_STOPPED` → `LINK_NOT_SENT` → 같은 `clientEventId` 재시도 확인 → `ALREADY_SENT` → `IN_PROGRESS` → `LIMIT_REACHED`다. 먼저 걸린 것을 돌려준다.
- 409의 `remainingAttempts`는 `IN_PROGRESS`만 남은 횟수이고 나머지는 0이다. `receiverLast4`는 `ALREADY_SENT`·`IN_PROGRESS`·`LIMIT_REACHED`에만 있고 나머지는 null이다.
- 링크와 다시 알림은 서로 다른 이벤트(`LINK_SENT`·`REMIND_SENT`)라 시도 횟수와 `ALREADY_SENT`를 따로 센다.
- 문자의 장소 이름은 기본 지점이면 지점 이름(`SPOT_NAME`), 임의 좌표면 계획에 저장된 장소 이름(`CUSTOM_NAME`)이다.

같은 `clientEventId`로 다시 부르는 경우는 이렇게 처리한다.

- 원래 요청이 끝났으면 문자를 다시 보내지 않고 원래 결과(200 또는 101)를 그대로 돌려준다. 이벤트도 늘지 않는다.
- 원래 요청이 아직 진행 중이면 409 `IN_PROGRESS`다. 차량의 5초 타임아웃 뒤 자동 재시도가 원래 요청보다 먼저 오는 경우다. 앱은 `DetailPickupLinkStatus`를 폴링해 결과를 확인한다.
- 결과 없이 30초가 지난 진행 중 이벤트는 실패로 세므로 같은 `clientEventId`를 다시 부르면 문자를 보내지 않고 101이 온다. 다시 보내려면 새 `clientEventId`를 쓴다.
- 이 재시도도 계획 판정(404, `PLAN_CLOSED`, `NO_SPOT`, `NO_CHILD_PHONE`, 다시 알림은 `NOT_STOPPED`, `LINK_NOT_SENT`)을 먼저 거친다. 그 사이 계획이 끝났으면 원래 결과 대신 그 오류가 온다.

중복 발송과 실패 처리:

- 서버는 사업자 호출 전에 결과가 없는 진행 중 이벤트(`DETAIL`의 `success`가 null)를 먼저 커밋하고 잠금 밖에서 보낸 뒤 결과를 채운다. 같은 계획에 다른 `clientEventId`로 동시에 온 요청은 이 이벤트를 보고 `IN_PROGRESS`가 되므로 문자는 한 번만 나간다. 요청이 끊겨도 발송과 결과 기록은 끝까지 한다.
- 서버가 사업자 접수 뒤에 실패하면(500) 이벤트가 진행 중으로 남고 `PickupLink:InFlightTimeoutSec`(30초)가 지나면 실패로 센다. 그 뒤 다시 보내면 아이가 같은 문자를 두 번 받을 수 있다. 못 받는 쪽이 더 위험하다고 보고 받아들인 동작이다(사용자 결정 2026-11-11).
- 사업자 접수(`sent` true)를 "보냄"으로 본다. 통신사 도달 여부는 알 수 없다.

`DetailPickupLinkStatus` 응답이다. 읽기 전용이다.

| code | 상황 | HTTP |
|---|---|---|
| 200 | 상태를 돌려준다 | 200 |
| 400 | `pickupPlanSeq`가 없거나 형식이 틀렸다 | 400 (`ProblemDetails`) |
| 401 | 인증 실패 | 401 |
| 404 | 계획이 없거나, 로그인한 계정의 계획이 아니거나, 끝났거나(`BOARDED`·`EXPIRED`·`CANCELED`), `EXPIRE_DT`가 지났다 | 404 |

- `data`는 `{ linkStatus, sentDt, remainingAttempts, opened, openedDt, leaving, leavingDt, leaveStatus, plannedLeaveDt, remindStatus }`다. `linkStatus`·`remindStatus`는 `NONE`(시도 없음), `SENDING`(진행 중), `SENT`(접수된 시도가 있음), `FAILED`(접수된 시도가 없고 마지막 시도가 실패. 결과 없이 30초가 지난 진행 중 발송 포함) 중 하나다. `sentDt`는 처음 접수된 링크 발송의 `EVENT_DT`이고, `remainingAttempts`는 링크 발송의 남은 횟수다.
- `leaving`·`leavingDt`는 아이 출발 알림 API가 기록한 `CHILD_LEAVING` 이벤트의 유무와 첫 이벤트의 `EVENT_DT`다. `opened`·`openedDt`(`LINK_OPENED`)를 기록하는 API는 아직 없어서 지금은 항상 false와 null이다.
- `leaveStatus`·`plannedLeaveDt`는 아래 "아이 출발 알림"의 출발 상태와 예정 시각이다. `plannedLeaveDt`는 `SCHEDULED`·`SOON`일 때만 값이 있다. 차량은 상태가 바뀐 순간에만 알린다(`SCHEDULED`가 되거나 `plannedLeaveDt`가 바뀌면 "22:03에 나온대요", `SOON`이 되면 "곧 나와요", `LEAVING`이 되면 "나오고 있어요").
- 필드·예시는 Notion "[차량] 픽업 링크 문자 발송"(https://app.notion.com/p/415aa638eaf881bbbd18cf5d4fd3de6d), "[차량] 정차 후 다시 알림 문자 발송"(https://app.notion.com/p/415aa638eaf881068dd4f1dbf92a31cd), "[차량] 픽업 링크 전달 상태 조회"(https://app.notion.com/p/415aa638eaf881a0a737d13571e9265c) 페이지가 정본이다.

## 아이 출발 알림

아이 휴대폰 웹(픽업 링크를 연 화면)이 부른다. 아이는 로그인하지 않으므로 인증 구간 `sign`이 없고, 링크 토큰으로 계획을 찾는다.

| 주소 | 용도 |
|---|---|
| `POST /api/v1/Pickup/W/AddChildLeave` | "지금 나가요"와 출발 시각 설정·변경("5분 뒤 나가요", "시간 직접 설정", "변경") |
| `DELETE /api/v1/Pickup/W/DeleteChildLeave` | 출발 시각 취소. 본문 없음 |

- 링크 토큰(링크 주소 마지막 43자)은 요청 헤더 `X-Pickup-Token`으로만 보낸다. URL·쿼리·본문에 싣지 않는다. 헤더가 없거나 형식(43자 base64url)이 틀려도 없는 링크와 같은 404다.
- `AddChildLeave` 본문은 출발 시각 설정·변경이면 `{ "leaveDt": "2026-11-12T22:03:00" }`, "지금 나가요"면 `{ "leaveNow": true }`다. 둘 중 정확히 하나만 보낸다. 둘 다 없거나(`{}`, `"leaveDt": ""`, 키 오타 포함) 둘 다 있거나 `leaveNow`가 false면 400이다. "지금 나가요"는 되돌릴 수 없어서 빈 값이 지금 나가요로 기록되지 않게 명시적인 값으로만 받는다. "5분 뒤 나가요"는 웹이 지금 + 5분을 계산해 보낸다.
- 본문은 필수이고 `Content-Type: application/json`이어야 한다. 본문이 없거나 JSON·날짜 형식이 틀리면 서비스까지 가지 않고 모델 검증 실패(`ProblemDetails` 400)가 나간다. Content-Type이 없으면 415다.
- `leaveDt`는 오프셋 없는 로컬(KST) 시각이다. 오프셋이나 `Z`가 붙으면 KST로 바꾼다. 초 이하는 버린다. 서버 현재 시각(분 단위 내림) − 1분 이상, 현재 + `PickupLink:LeaveMaxAheadMin`(60)분 이하, 계획의 `EXPIRE_DT` 전이어야 한다.
- "지금 나가요"는 한 번만 기록되고 되돌릴 수 없다. 출발 시각은 그 전까지 몇 번이고 바꾸거나 취소할 수 있다.
- 같은 요청이 다시 오면(이미 지금 나가요, 같은 시각으로 다시 설정, 이미 취소된 상태에서 취소) 이벤트를 늘리지 않고 200과 지금 상태를 돌려준다.
- 응답 `data`는 `{ reason, leaveStatus, plannedLeaveDt, leavingDt }`다. 웹은 이 값으로 출발 시간 카드와 버튼을 그린다.

| code | 상황 | HTTP |
|---|---|---|
| 200 | 처리했거나, 같은 요청이라 바꿀 것이 없었다 | 200 |
| 400 | `leaveNow`·`leaveDt` 조합이 틀렸거나 `leaveDt`가 범위 밖이다. `data`는 null. 본문 형식 오류는 `ProblemDetails`다. `AddChildLeave`만 | 400 |
| 404 | 토큰 헤더가 없거나 형식이 틀렸거나 없는 링크다. `data`는 null | 404 |
| 409 | 처리할 수 없는 상태. `data.reason`에 사유가 들어간다 | 409 |
| 429 | 요청 빈도 제한(클라이언트마다 1분 30회). 본문 없음, `Retry-After` 헤더 | 429 |

| `reason` (409) | 상황 |
|---|---|
| `LINK_CLOSED` | 계획이 탑승·만료·취소로 끝났거나 `EXPIRE_DT`가 지났거나 링크가 닫혔다. 웹은 "이 링크는 끝났어요" 화면을 보여준다. `data`의 나머지는 `leaveStatus` `NONE`, 시각 null로 고정이다 |
| `ALREADY_LEAVING` | 이미 "지금 나가요"를 눌러 출발 시각을 바꾸거나 취소할 수 없다. `data`에 지금 상태(`LEAVING`, `leavingDt`)가 들어 있다 |

출발 상태 `leaveStatus`는 저장하지 않고 이벤트 이력과 서버 현재 시각으로 매번 계산한다.

| 값 | 뜻 |
|---|---|
| `NONE` | 출발 알림이 없거나 취소됐다 |
| `SCHEDULED` | 출발 시각이 정해졌고 아직 `PickupLink:LeaveSoonMin`(1)분 넘게 남았다 |
| `SOON` | 출발 시각 1분 전부터. 시각이 지나도 아이가 바꾸거나 취소하거나 "지금 나가요"를 누르기 전에는 그대로다 |
| `LEAVING` | "지금 나가요"를 눌렀다 |

- 필드·예시는 Notion "[웹] 아이 출발 알림"(https://app.notion.com/p/416aa638eaf881beab73d70736cd6c39), "[웹] 아이 출발 시각 취소"(https://app.notion.com/p/416aa638eaf881e6a672c69124ffa4ae) 페이지가 정본이다.

## 시간표 삭제

- 주소는 `DELETE /api/v1/Schedule/M/sign/DeleteSchedule`다. 인증 구간 `sign`이 있어 Bearer가 필요하고, 사용자는 JWT의 `sub`에서 꺼낸다. JSON 본문 `{ "scheduleSeq": 12 }`를 보낸다(`DeleteQrLogin`과 같이 DELETE에 본문을 싣는다).
- 시간표와 그 요일·날짜별 예외를 함께 삭제 표시한다. 되돌릴 수 없다. 처리 순서와 잠금은 `architecture/schema.md`의 `TIMETABLE` 절이다.

| code | 상황 | HTTP |
|---|---|---|
| 200 | 지웠다. `data.scheduleSeq`는 지운 시간표 ID다 | 200 |
| 400 | `scheduleSeq`가 없거나 1보다 작다. 빠지면 `ProblemDetails`다 | 400 |
| 401 | 인증 실패이거나 계정이 없다 | 401 |
| 404 | 시간표가 없거나, 로그인한 계정 아이의 시간표가 아니거나, 이미 지웠다. 셋을 구분하지 않는다 | 404 |
| 409 | 이 시간표의 요일을 가리키는 진행 중 픽업 계획(`PLANNED`·`STARTED`·`WAITING`·`STOPPED`·`LOOPING`)이 있다. `data`는 null이다 | 409 |

- 400·401·409의 `message`는 `잘못된 요청입니다.`, 404는 `대상을 찾을 수 없습니다.`다(시간표 API의 기존 404 문구). 앱은 `code`로 나눈다.
- 진행 중인지는 계획의 `STATUS`로만 본다. `EXPIRE_DT`가 지났는데 만료 처리가 아직 돌지 않은 계획도 진행 중으로 보고 409다.

## 시간표 편집

- 주소는 `PATCH /api/v1/Schedule/M/sign/UpdateSchedule`다. 인증 구간 `sign`이 있어 Bearer가 필요하고, 사용자는 JWT의 `sub`에서 꺼낸다. JSON 본문은 `scheduleSeq`와 `AddSchedule`의 필드 전부(`displayName`·`place`·`days`·`endTime`·`confirmOverlap`)다.
- 시간표 전체를 보낸 값으로 바꾼다. "빠진 필드는 바꾸지 않음" 규칙(위 라우트 절)의 예외이며, 빠진 필수 필드는 `ProblemDetails` 400이다. 검증 규칙과 학원 찾기·만들기는 `AddSchedule`과 같다. 요일 처리와 잠금은 `architecture/schema.md`의 `TIMETABLE` 절이다.
- 응답 `data`는 `AddSchedule`과 같은 `{ schedule, overlaps }`다.

| code | 상황 | HTTP |
|---|---|---|
| 200 | 바꿨다. `data.schedule`은 바꾼 시간표다 | 200 |
| 103 | 같은 요일에 하원 시각이 40분 이하로 가까운 다른 시간표가 있다. 저장하지 않고 `data.overlaps`만 준다. 자기 자신은 세지 않는다. `confirmOverlap: true`로 다시 보내면 저장한다 | 200 |
| 104 | 좌표가 학원가 밖이다 | 200 |
| 400 | 값이 없거나 검증에 실패했다. 빠지면 `ProblemDetails`다 | 400 |
| 401 | 인증 실패이거나 계정이 없다 | 401 |
| 404 | 시간표가 없거나, 로그인한 계정 아이의 시간표가 아니거나, 이미 지웠다. 셋을 구분하지 않는다 | 404 |
| 409 | 진행 중 픽업 계획이 있는데 학원이나 하원 시각을 바꾸거나 그 계획의 요일을 뺐다. 이름 변경과 다른 요일 추가·빼기는 된다. `data`는 null이다 | 409 |

- 판정 순서는 400 → 401 → 404 → 104 → 409 → 103이다. 409를 103보다 먼저 봐서 겹침을 확인하고 다시 보냈는데 409가 나오는 일이 없다.
- `message`는 103이 `같은 요일에 시간이 가까운 시간표가 있어요.`, 104가 `지금은 대치동 학원만 등록할 수 있어요`로 `AddSchedule`과 같고, 404는 `대상을 찾을 수 없습니다.`, 400·401·409는 `잘못된 요청입니다.`다.
- 학원이 바뀌었는지는 정규화한 이름·주소로 찾은 학원 행(`ACADEMY_ID`)이 지금과 다른지로 본다. 같은 학원에 좌표만 채우는 것은 학원 변경이 아니다.

## 커서 페이징

- 목록은 오프셋 페이징을 쓰지 않고 `CursorResult<T>`(`items`·`lastCursor`·`hasMore`)로 돌려준다.
- 커서는 PK 내림차순이다. `AND t.{PK} < @lastSeq`로 이어 받는다.
- `pageSize`는 1~100이다. 범위를 벗어나면 400을 돌려준다.
- `pageSize + 1`개를 조회해 초과분이 있으면 `hasMore = true`로 두고 초과분은 잘라낸다. `COUNT(*)`를 따로 실행하지 않는다.
- `lastCursor`는 반환한 마지막 항목의 PK다.

## API 명세

- API 문서의 정본은 Notion "라이드메이트 API 문서"(https://app.notion.com/p/415aa638eaf880808d84c4ffba30bb51)다. 목차 아래에 화면별 API 호출 순서(로그인·회원가입), API 목록과 API별 하위 페이지("API 상세", Request·Response·Error·예외 네 절), 공통 오류 형식이 있다. 주소·응답 형식 같은 공통 규칙은 이 문서가 정본이다. 레포에는 API별 문서를 두지 않는다.
- 필드 이름·타입의 기계 정본은 루트 `docs/api/openapi.json`(서버 Swagger 출력)이다. Notion 문서와 다르면 `openapi.json`이 맞고 Notion을 고친다.
- 라우트·DTO를 바꾼 작업은 같은 PR에서 `openapi.json`을 갱신하고 Notion 문서를 고친다. 응답 `code`·`message`·오류 상황만 바꾼 작업도 Notion 문서를 고친다. 절차는 `update-api-spec` 스킬을 따른다.
