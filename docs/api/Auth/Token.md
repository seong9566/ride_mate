# 토큰 발급

`POST /api/v1/Auth/M/Token`

차량 로그인 토큰(access·refresh)을 발급하거나 교체한다. OAuth 2.0 형식이라 공통 응답 형식(`{ message, data, code }`)으로 감싸지 않는다.

## Header

- Authorization(Bearer jwt) : 불필요
- Content-Type : `application/x-www-form-urlencoded` (JSON이 아니다)

## Request

form 필드다. 이름은 OAuth 규약대로 snake_case다.

- grant_type (string): 발급 방식. 지금은 `refresh_token`만 지원한다 — 필수
- refresh_token (string): 갖고 있는 refresh 토큰 — `grant_type`이 `refresh_token`이면 필수

```text
grant_type=refresh_token&refresh_token=Qm9vZ2llV29vZ2llQm9vZ2llV29vZ2llQm9vZ2llV28
```

## Response

응답 헤더에 `Cache-Control: no-store`가 붙는다.

- access_token (string): access 토큰(JWT, HS256). 유효 시간 1시간
- token_type (string): 항상 `Bearer`
- expires_in (integer): access 토큰 유효 시간(초). 3600
- refresh_token (string): 새 refresh 토큰(base64url 43자). 유효 기간 90일이고 refresh할 때마다 그 시점부터 다시 90일이 된다. 쓸 때마다 새 값으로 바뀐다
- session_id (integer): 로그인 세션 ID. 교체해도 그대로다
- setup_required (boolean): 초기 설정이 필요한지. 운전자 이름이 비었거나 등록된 아이가 없으면 `true`

**\<Success>**

```json
{
  "access_token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxIn0.c2lnbmF0dXJl",
  "token_type": "Bearer",
  "expires_in": 3600,
  "refresh_token": "Qm9vZ2llV29vZ2llQm9vZ2llV29vZ2llQm9vZ2llV28",
  "session_id": 12,
  "setup_required": true
}
```

## Error

HTTP 400이다. 공통 형식으로 감싸지 않는다. 앱은 `error` 값으로 분기한다.

- error (string): 오류 코드. 아래 "예외"의 값 중 하나
- error_description (string): 오류 설명(영어 한 줄)
- remaining_attempts (integer): 남은 인증번호 시도 횟수. 지금은 보내지 않는다(값이 없으면 필드가 빠진다)
- retry_after (integer): 잠금이 풀리기까지 남은 시간(초). 지금은 보내지 않는다

```json
{
  "error": "invalid_grant",
  "error_description": "refresh token is invalid or expired"
}
```

## 예외

1. `invalid_request`: `grant_type`이 없거나, `refresh_token` 방식인데 `refresh_token`이 없다. 400
2. `unsupported_grant_type`: `refresh_token`이 아닌 `grant_type`(예: `otp`). 400
3. `invalid_grant`: refresh 토큰이 틀렸거나(128자보다 길어도 같다) 만료됐거나, 로그아웃된 세션이거나, 계정이 삭제됐다. 400
4. 서버 에러: 500 Internal Server Error (공통 오류 형식)

- 앱은 `invalid_grant`만 "로그아웃됨"으로 처리한다. 네트워크 오류, 5xx, 429는 같은 refresh 값으로 다시 보낸다.
- 새 refresh 값을 저장하기 전에는 옛 값을 버리지 않는다. 교체 직전 값은 24시간 동안 받아 주고, 다시 보내면 이미 발급한 새 값을 그대로 돌려준다.
- 세션 하나에서 refresh 요청은 한 번에 하나만 보낸다.
