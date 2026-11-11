# QR 로그인 취소(휴대폰 웹)

`DELETE /api/v1/Auth/W/sign/DeleteQrLogin`

휴대폰에서 진행 중인 QR 로그인을 취소하고 취소 사유를 `PHONE`으로 기록한다.

## Header

- Authorization(Bearer jwt) : 쿠키(`__Host-rm_web_session`)
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

data (object): QR 취소 결과

- qrState (string): `CANCELED`
- cancelReason (string): `PHONE`, 또는 이미 취소된 요청의 기존 사유
- signedIn (boolean): `true`
- signupRequired (boolean): `false`

**\<Success>**

```json
{
  "message": "요청이 정상 처리되었습니다.",
  "data": {
    "qrState": "CANCELED",
    "cancelReason": "PHONE",
    "signedIn": true,
    "signupRequired": false
  },
  "code": 200
}
```

## Error

- qrState (string): `NOT_FOUND` 또는 `USED`

```json
{
  "message": "잘못된 요청입니다.",
  "data": {
    "qrState": "USED",
    "signedIn": true,
    "signupRequired": false
  },
  "code": 409
}
```

## 예외

1. 인증 실패: 웹 세션 쿠키가 없거나 만료됐다. 401 (공통 오류 형식)
2. 요청 없음: 404, code 404, `qrState` `NOT_FOUND`
3. 이미 사용함·다른 계정: 409, code 409, `qrState` `USED`
4. 이미 취소됨: 기존 취소 사유를 유지하고 성공으로 본다. 200, code 200
5. 만료됨: 409, code 409, `qrState` `EXPIRED`
6. 본인 확인 전: 이 계정으로 `VERIFIED`가 되기 전(`PENDING`·`SCANNED`)에는 취소할 수 없다. 409, code 409, `qrState`는 지금 상태
7. 서버 에러: 500 Internal Server Error (공통 오류 형식)

- 휴대폰의 취소 버튼은 허용 화면에만 있으므로, 이 계정으로 본인 확인을 마친 QR만 취소한다.
