# 토큰 발급

`POST /api/v1/Auth/M/Token`

차량 로그인 토큰(access·refresh)을 발급하거나 교체한다. 인증번호 로그인(`otp`), QR 로그인(`urn:ietf:params:oauth:grant-type:device_code`), refresh 교체(`refresh_token`)를 같은 주소에서 `grant_type`으로 나눈다. OAuth 2.0 형식이라 공통 응답 형식(`{ message, data, code }`)으로 감싸지 않는다.

## Header

- Authorization(Bearer jwt) : 불필요
- Content-Type : `application/x-www-form-urlencoded` (JSON이 아니다)

## Request

form 필드다. 이름은 OAuth 규약대로 snake_case다.

- grant_type (string): 발급 방식. `otp`(인증번호 로그인), `urn:ietf:params:oauth:grant-type:device_code`(QR 로그인), `refresh_token`(교체) — 필수
- phone (string): 숫자만 쓴 휴대폰 번호. 형식 `^01[016789][0-9]{7,8}$` — `grant_type`이 `otp`이면 필수
- code (string): [인증번호 발송](AddPhoneVerification.md)으로 받은 숫자 6자리 — `grant_type`이 `otp`이면 필수
- device_id (string): 앱이 처음 실행할 때 만든 설치 번호(UUID 등, 차대번호 아님). 로그인마다 같은 값. 1~100자 — `grant_type`이 `otp` 또는 `urn:ietf:params:oauth:grant-type:device_code`이면 필수
- device_code (string): [QR 로그인 발급](DeviceAuthorization.md)에서 차량이 받은 값. 최대 128자 — `grant_type`이 `urn:ietf:params:oauth:grant-type:device_code`이면 필수
- refresh_token (string): 갖고 있는 refresh 토큰 — `grant_type`이 `refresh_token`이면 필수

쓰지 않는 방식의 필드는 보내지 않는다(보내도 무시한다).

```text
grant_type=otp&phone=01012345678&code=482173&device_id=3f2b8c1e-9a4d-4e7b-b1c2-7d9e0f6a5b43
```

```text
grant_type=refresh_token&refresh_token=Qm9vZ2llV29vZ2llQm9vZ2llV29vZ2llQm9vZ2llV28
```

```text
grant_type=urn%3Aietf%3Aparams%3Aoauth%3Agrant-type%3Adevice_code&device_code=AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA&device_id=3f2b8c1e-9a4d-4e7b-b1c2-7d9e0f6a5b43
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
- remaining_attempts (integer): 잠기기 전까지 남은 인증번호 시도 횟수. `otp_mismatch`일 때만 들어간다(값이 없으면 필드가 빠진다)
- retry_after (integer): 잠금이 풀리기까지 남은 시간(초). `otp_locked`일 때만 들어간다
- qr_status (string): QR 로그인 진행 상태. `authorization_pending`일 때만 `PENDING`, `SCANNED`, `VERIFIED` 중 하나가 들어간다
- cancel_reason (string): QR 로그인 취소 사유. `access_denied`일 때만 `USER`, `DRIVING`, `PHONE` 중 하나가 들어간다

```json
{
  "error": "otp_mismatch",
  "error_description": "verification code is incorrect",
  "remaining_attempts": 4
}
```

```json
{
  "error": "authorization_pending",
  "error_description": "authorization is pending",
  "qr_status": "SCANNED"
}
```

```json
{
  "error": "access_denied",
  "error_description": "authorization request was canceled",
  "cancel_reason": "USER"
}
```

## 예외

1. `invalid_request`: `grant_type`이 없거나, 그 방식에 필요한 값이 없거나 형식이 틀렸다. 400
2. `unsupported_grant_type`: `otp`·`refresh_token`·`urn:ietf:params:oauth:grant-type:device_code`가 아닌 `grant_type`. 400
3. `invalid_grant`(`refresh_token`): refresh 토큰이 틀렸거나(128자보다 길어도 같다) 만료됐거나, 로그아웃된 세션이거나, 계정이 삭제됐다. 400
4. `invalid_grant`(`otp`): 대기 중인 로그인 인증번호가 없다. 받지 않았거나 이미 썼다(인증에 성공했으면 결과와 관계없이 그 인증번호는 다시 쓸 수 없다). 400
5. `otp_mismatch`(`otp`): 인증번호가 틀렸다. `remaining_attempts`가 함께 온다. 400
6. `otp_locked`(`otp`): 5번 틀려 10분 동안 잠겼다. `retry_after`가 함께 온다. 400
7. `otp_expired`(`otp`): 인증번호를 받은 지 3분이 지났다. 400
8. `signup_required`(`otp`): 인증번호는 맞지만 가입한 계정이 없다. 계정을 만들지 않는다(가입은 휴대폰 웹). 400
9. `authorization_pending`(`device_code`): 아직 승인되지 않았다. `qr_status`가 함께 온다. 5초 뒤 다시 조회한다. 400
10. `slow_down`(`device_code`): 마지막 조회 뒤 5초가 지나기 전에 다시 조회했다. 400
11. `expired_token`(`device_code`): 승인되지 않은 채 QR 발급 후 3분이 지났다. 400
12. `access_denied`(`device_code`): 차량이나 휴대폰에서 QR 로그인을 취소했다. `cancel_reason`이 함께 온다. 400
13. `invalid_grant`(`device_code`): `device_code`가 틀렸거나 이미 토큰을 받았거나, 발급 때와 다른 `device_id`이거나, 승인된 계정을 확인할 수 없다. 400
14. 서버 에러: 500 Internal Server Error (공통 오류 형식)

- 같은 기기(`device_id`)·같은 계정으로 다시 로그인하면 그 계정·기기의 직전 차량 세션이 끝난다.
- QR 로그인은 `DeviceAuthorization` 응답의 `interval`(5초)보다 자주 조회하지 않는다. `device_code`는 QR이나 휴대폰에 보내지 않고 발급받은 차량만 보관한다.
- 앱은 `refresh_token` 방식의 `invalid_grant`만 "로그아웃됨"으로 처리한다. `otp`의 `invalid_grant`는 인증번호를 다시 받게 한다. 네트워크 오류, 5xx, 429는 같은 refresh 값으로 다시 보낸다.
- 새 refresh 값을 저장하기 전에는 옛 값을 버리지 않는다. 교체 직전 값은 24시간 동안 받아 주고, 다시 보내면 이미 발급한 새 값을 그대로 돌려준다.
- 세션 하나에서 refresh 요청은 한 번에 하나만 보낸다.
