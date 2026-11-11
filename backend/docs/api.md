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

500과 모델 검증 실패(400)를 뺀 응답 본문은 `{ message, data, code }` 형태다.

| code | 의미 | HTTP |
|---|---|---|
| 200 | 정상 처리 | 200 |
| 101·102·103·201·202 | 도메인별 부분 성공·분기 | 200 |
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

- `session_id`는 `LOGIN_SESSION_ID`다. `setup_required`는 그 계정의 운전자 이름이 비었거나 살아 있는 아이가 없으면 `true`다.
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

## 커서 페이징

- 목록은 오프셋 페이징을 쓰지 않고 `CursorResult<T>`(`items`·`lastCursor`·`hasMore`)로 돌려준다.
- 커서는 PK 내림차순이다. `AND t.{PK} < @lastSeq`로 이어 받는다.
- `pageSize`는 1~100이다. 범위를 벗어나면 400을 돌려준다.
- `pageSize + 1`개를 조회해 초과분이 있으면 `hasMore = true`로 두고 초과분은 잘라낸다. `COUNT(*)`를 따로 실행하지 않는다.
- `lastCursor`는 반환한 마지막 항목의 PK다.

## API 명세

- 앱↔서버 계약은 루트 `docs/api/openapi.json`이다. 모빌리티 앱은 이 파일을 보고 개발한다.
- API마다 `docs/api/{도메인}/{액션}.md`를 둔다. 양식은 Header·Request·Response·Error·예외 다섯 절이고, 템플릿은 `update-api-spec` 스킬의 `templates/api-doc.md`다.
- 라우트·DTO를 바꾼 작업은 같은 PR에서 명세와 API 문서를 갱신한다. 응답 `code`·`message`·오류 상황만 바꾼 작업도 API 문서를 갱신한다. 절차는 `update-api-spec` 스킬을 따른다.
