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

## `Token` 예외

토큰 발급 API는 OAuth 2.0 형식을 따르므로 위 규약에서 다음이 다르다. 다른 API는 영향이 없다.

- 주소는 `POST /api/v1/Auth/M/Token`이다. OAuth 토큰 발급이라 `Add` 접두를 쓰지 않는다. 이 API는 DB에 행을 하나 추가하는 것이 아니라 토큰을 발급·교체한다. 인증 구간이 없어(`sign` 아님) Bearer 없이 부른다.
- 요청 본문은 JSON이 아니라 `application/x-www-form-urlencoded`이고, 필드 이름은 OAuth 규약대로 snake_case다.
- 지금 지원하는 `grant_type`은 `refresh_token` 하나다(`refresh_token` 값을 함께 보낸다). `otp`와 `urn:ietf:params:oauth:grant-type:device_code`는 카드 T08·T09에서 더하며, 그 전에는 `unsupported_grant_type`을 돌려준다.
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
- 실패(400)도 감싸지 않고 `{ "error": "...", "error_description": "..." }`를 돌려준다. `error_description`은 영어 한 줄이고 앱은 `error` 값으로 분기한다.

| `error` | 상황 |
|---|---|
| `invalid_request` | `grant_type`이나 그 방식에 필요한 값이 빠졌다 |
| `unsupported_grant_type` | 지원하지 않는 `grant_type` |
| `invalid_grant` | refresh 토큰이 틀렸거나(128자보다 길어도 같다) 만료됐거나 로그아웃된 세션이거나 계정이 삭제됐다 |

- 서버 오류(500)는 다른 API와 같은 `ProblemDetails`다.
- `Token`은 요청 DTO에 검증 특성을 붙이지 않아 값이 빠져도 `[ApiController]`의 `ProblemDetails` 400이 아니라 `invalid_request`가 나간다(`architecture/dto.md`).

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
