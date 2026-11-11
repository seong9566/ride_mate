# QR 로그인 진입(휴대폰 웹)

`GET /api/v1/Auth/W/DetailQrLogin`

QR 주소를 열고 현재 상태를 확인한다. 유효한 웹 세션 쿠키가 있으면 그 계정으로 본인 확인 완료 상태까지 진행한다.

## Header

- Authorization(Bearer jwt) : 불필요. `__Host-rm_web_session` 쿠키는 선택

## Request

query 값이다.

- userCode (string): QR 주소에 담긴 코드. 1~64자 — 필수

```text
?userCode=AAAAAAAAAAAAAAAAAAAAAA
```

## Response

data (object): QR 진행 상태와 휴대폰 화면 표시 값

- qrState (string): `SCANNED` 또는 `VERIFIED`
- signedIn (boolean): 웹 세션으로 계정을 확인했는지
- signupRequired (boolean): 이 응답에서는 `false`
- userName (string): 운전자 이름. `VERIFIED`일 때만 있음
- maskedPhone (string): 가린 번호. `VERIFIED`일 때만 있음
- matchCode (string): 차와 비교할 숫자 4자리. 본인 확인 뒤에만 있음
- expiresIn (integer): QR 만료까지 남은 초

**\<Success>**

```json
{
  "message": "요청이 정상 처리되었습니다.",
  "data": {
    "qrState": "VERIFIED",
    "signedIn": true,
    "signupRequired": false,
    "userName": "엄마",
    "maskedPhone": "010-****-5678",
    "matchCode": "0427",
    "expiresIn": 132
  },
  "code": 200
}
```

## Error

- qrState (string): `NOT_FOUND`, `USED`, `CANCELED`, `EXPIRED`
- cancelReason (string): 취소 상태일 때 `USER`, `DRIVING`, `PHONE`

```json
{
  "message": "잘못된 요청입니다.",
  "data": {
    "qrState": "EXPIRED",
    "signedIn": false,
    "signupRequired": false
  },
  "code": 409
}
```

## 예외

1. 요청 없음: `userCode`에 해당하는 QR이 없다. 404, code 404, `qrState` `NOT_FOUND`
2. 이미 끝남: 이미 허용·사용됐거나, 다른 계정이 본인 확인(`VERIFIED`)을 마친 QR이다. 쿠키 없이 열었을 때도 같다. 409, code 409, `qrState` `USED`
3. 취소됨: 차량 또는 휴대폰에서 취소했다. 409, code 409, `qrState` `CANCELED`
4. 만료됨: QR의 3분 유효 시간이 지났다. 409, code 409, `qrState` `EXPIRED`
5. 요청 검증 실패: 값이 없거나 64자를 넘는다. 400 (공통 오류 형식)
6. 서버 에러: 500 Internal Server Error (공통 오류 형식)
