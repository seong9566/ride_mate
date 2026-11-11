# QR 로그인 발급

`POST /api/v1/Auth/M/DeviceAuthorization`

차량에 표시할 QR 로그인 요청을 만든다. OAuth 2.0 기기 인증 형식이라 공통 응답 형식(`{ message, data, code }`)으로 감싸지 않는다.

## Header

- Authorization(Bearer jwt) : 불필요
- Content-Type : `application/x-www-form-urlencoded` (JSON이 아니다)

## Request

form 필드다. 이름은 OAuth 규약대로 snake_case다.

- device_id (string): 앱이 처음 실행할 때 만든 설치 번호(UUID 등, 차대번호 아님). 1~100자 — 필수

```text
device_id=3f2b8c1e-9a4d-4e7b-b1c2-7d9e0f6a5b43
```

## Response

응답 헤더에 `Cache-Control: no-store`가 붙는다.

- device_code (string): 차량이 승인 결과를 조회할 때 쓰는 base64url 43자 코드. 차량만 보관한다
- user_code (string): QR 주소에 들어가는 base64url 22자 코드
- verification_uri (string): QR 로그인 기본 주소
- verification_uri_complete (string): `user_code`를 `code` query로 포함한 완성 주소
- expires_in (integer): QR 로그인 유효 시간(초). 180
- interval (integer): [토큰 발급](Token.md)의 `device_code` 방식을 조회할 최소 간격(초). 3
- match_code (string): 차와 휴대폰에서 비교하는 숫자 4자리 문자열

**\<Success>**

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

## Error

HTTP 400이다. 공통 형식으로 감싸지 않는다.

- error (string): 오류 코드. 입력이 잘못되면 `invalid_request`
- error_description (string): 오류 설명(영어 한 줄)

```json
{
  "error": "invalid_request",
  "error_description": "device_id is required"
}
```

## 예외

1. `invalid_request`: `device_id`가 없거나 공백이거나 100자를 넘는다. 400
2. 서버 에러: 500 Internal Server Error (공통 오류 형식)

- `device_code`는 QR에 넣거나 휴대폰으로 보내지 않는다. 토큰을 받을 차량만 보관한다.
- QR에는 `verification_uri_complete`를 넣고, 차 화면에는 `match_code`를 함께 보여준다.
