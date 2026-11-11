# API 규약

## 라우트

`/api/{버전}/{도메인}/{클라이언트}/{인증구간}/{액션}` — 예: `/api/v1/Pickup/M/sign/GetPickupList`

| 세그먼트 | 값 |
|---|---|
| `{버전}` | `v1` |
| `{도메인}` | PascalCase 단수 (`Pickup`, `User`) |
| `{클라이언트}` | `M` 하나만 쓴다 |
| `{인증구간}` | 인증이 필요한 API는 `sign`. 인증 방식은 `security.md` |
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

## 로그인·토큰 갱신

- 인증번호 로그인 주소는 `POST /api/v1/Auth/M/AddLoginSession`, 토큰 갱신 주소는 `PATCH /api/v1/Auth/M/UpdateLoginSession`이다. 둘 다 인증 구간이 없어(`sign` 아님) Bearer 없이 부른다.
- 요청 본문은 `application/json`이다. 인증번호 로그인은 다음 값을 보낸다.

```json
{
  "phone": "01012345678",
  "code": "123456",
  "deviceId": "3f2b8c1e-..."
}
```

- 토큰 갱신은 다음 값을 보낸다.

```json
{
  "refreshToken": "…"
}
```

- `deviceId`는 앱이 처음 실행할 때 만든 임의의 설치 번호(UUID 등)다. 차대번호가 아니며 로그인마다 같은 값을 보낸다. 같은 기기·같은 계정으로 다시 로그인하면 그 기기의 직전 차량 세션이 끝난다.
- 성공(200)은 다음 공통 응답 형태이고 `Cache-Control: no-store`가 붙는다.

```json
{
  "message": "요청이 정상 처리되었습니다.",
  "data": {
    "accessToken": "…",
    "tokenType": "Bearer",
    "expiresIn": 3600,
    "refreshToken": "…",
    "sessionId": 12,
    "setupRequired": true
  },
  "code": 200
}
```

- `sessionId`는 `LOGIN_SESSION_ID`다. `setupRequired`는 그 계정의 운전자 이름이 비었거나 살아 있는 아이가 없으면 `true`다.
- 실패는 HTTP 400이고 다음 공통 응답 형태다. 앱은 `data.error` 값으로 분기한다.

```json
{
  "message": "잘못된 요청입니다.",
  "data": {
    "error": "otp_mismatch",
    "remainingAttempts": 4
  },
  "code": 400
}
```

| `error` | API | 상황 |
|---|---|---|
| `invalid_request` | 둘 다 | Service를 직접 불렀을 때 필요한 값이 빠졌거나 형식이 틀렸다 |
| `invalid_refresh_token` | `UpdateLoginSession` | refresh 토큰이 틀렸거나 만료됐거나 로그아웃된 세션이거나 계정이 삭제됐다. 앱은 로그아웃 처리한다 |
| `otp_not_requested` | `AddLoginSession` | 대기 중인 로그인 인증번호가 없다. 인증번호를 받지 않았거나 이미 썼다 |
| `otp_locked` | `AddLoginSession` | 인증번호 입력이 잠겼다. `retryAfter`에 잠금이 풀리기까지 남은 초가 들어간다 |
| `otp_expired` | `AddLoginSession` | 인증번호가 만료됐다 |
| `otp_mismatch` | `AddLoginSession` | 인증번호가 틀렸다. `remainingAttempts`에 잠기기 전까지 남은 횟수가 들어간다 |
| `signup_required` | `AddLoginSession` | 계정이 없다. 휴대폰 웹에서 가입해야 한다 |

- 서버 오류(500)는 다른 API와 같은 `ProblemDetails`다.
- 모델 검증에 실패하면 다른 API처럼 `ProblemDetails` 400이 나간다. `refreshToken`이 128자를 넘어도 같다.

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
