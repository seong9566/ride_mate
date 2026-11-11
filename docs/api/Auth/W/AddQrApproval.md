# QR 로그인 허용(휴대폰 웹)

`POST /api/v1/Auth/W/sign/AddQrApproval`

휴대폰과 차의 숫자 4자리가 같은지 확인한 뒤 차량 로그인을 허용한다.

## Header

- Authorization(Bearer jwt) : 쿠키(`rm_web_session`)
- Content-Type : `application/json`

## Request

JSON 본문이다.

- userCode (string): QR 주소에 담긴 코드. 1~64자 — 필수

```json
{
  "userCode": "AAAAAAAAAAAAAAAAAAAAAA"
}
```

## Response

data (object): QR 허용 결과

- qrState (string): `APPROVED`
- signedIn (boolean): `true`
- signupRequired (boolean): `false`

**\<Success>**

```json
{
  "message": "요청이 정상 처리되었습니다.",
  "data": {
    "qrState": "APPROVED",
    "signedIn": true,
    "signupRequired": false
  },
  "code": 200
}
```

## Error

- qrState (string): `NOT_FOUND`, `SCANNED`, `PENDING`, `USED`, `CANCELED`, `EXPIRED`

```json
{
  "message": "잘못된 요청입니다.",
  "data": {
    "qrState": "SCANNED",
    "signedIn": false,
    "signupRequired": false
  },
  "code": 409
}
```

## 예외

1. 인증 실패: 웹 세션 쿠키가 없거나 만료됐다. 401 (공통 오류 형식)
2. 본인 확인 전: 상태가 `SCANNED` 또는 `PENDING`이다. 409, code 409
3. 요청 없음: 404, code 404, `qrState` `NOT_FOUND`
4. 이미 끝남·다른 계정: 409, code 409, `qrState` `USED`
5. 취소·만료: 409, code 409, `qrState` `CANCELED` 또는 `EXPIRED`
6. 서버 에러: 500 Internal Server Error (공통 오류 형식)
