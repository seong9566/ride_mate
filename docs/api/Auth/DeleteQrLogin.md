# QR 로그인 취소(차량)

`DELETE /api/v1/Auth/M/DeleteQrLogin`

차량이 아직 사용하지 않은 QR 로그인 요청을 취소한다. 사용자 취소와 차량 출발 취소를 구분해 기록한다.

## Header

- Authorization(Bearer jwt) : 불필요
- Content-Type : `application/json`

## Request

JSON 본문이다.

- deviceCode (string): QR 발급 때 차량이 받은 `device_code`. 1~128자 — 필수
- deviceId (string): QR 발급 때 보낸 차량 앱 설치 번호. 1~100자 — 필수
- reason (string): 취소 사유. `USER` 또는 `DRIVING` — 필수

```json
{
  "deviceCode": "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA",
  "deviceId": "3f2b8c1e-9a4d-4e7b-b1c2-7d9e0f6a5b43",
  "reason": "USER"
}
```

## Response

data (boolean): 처리 결과. 성공하거나 이미 취소된 요청이면 `true`

**\<Success>**

```json
{
  "message": "요청이 정상 처리되었습니다.",
  "data": true,
  "code": 200
}
```

## Error

- data (boolean): 처리 실패. `false`
- code (integer): 요청을 찾지 못하면 404, 이미 사용한 요청이면 409

```json
{
  "message": "잘못된 요청입니다.",
  "data": false,
  "code": 404
}
```

## 예외

1. 요청 검증 실패: 필수 값이 없거나 길이·`reason` 형식이 맞지 않는다. 400 (공통 오류 형식)
2. 요청 없음: `deviceCode`를 찾을 수 없거나 발급 때와 `deviceId`가 다르다. 404, code 404
3. 이미 사용함: QR 로그인으로 차량 토큰을 이미 받았다. 409, code 409
4. 이미 취소됨: 값을 바꾸지 않고 성공으로 본다. 200, code 200, `data` true
5. 서버 에러: 500 Internal Server Error (공통 오류 형식)
