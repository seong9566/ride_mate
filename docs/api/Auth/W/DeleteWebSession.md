# 다른 번호로 로그인(휴대폰 웹)

`DELETE /api/v1/Auth/W/sign/DeleteWebSession`

현재 웹 세션을 끝낸다. 진행 중인 QR 코드를 함께 보내면 같은 계정의 본인 확인 상태를 되돌려 다른 번호로 인증할 수 있게 한다.

## Header

- Authorization(Bearer jwt) : 쿠키(`__Host-rm_web_session`)
- Content-Type : `application/json`

## Request

JSON 본문이다. 본문 없이 불러도 된다(`userCode` 없음과 같다).

- userCode (string): 되돌릴 QR의 코드. 최대 64자 — 선택

```json
{
  "userCode": "AAAAAAAAAAAAAAAAAAAAAA"
}
```

## Response

data (object): 세션 종료와 QR 되돌림 결과

- qrState (string): QR을 되돌렸으면 `SCANNED`, 아니면 필드가 없음
- signedIn (boolean): `false`
- signupRequired (boolean): `false`

성공하면 `__Host-rm_web_session` 쿠키를 지운다.

**\<Success>**

```json
{
  "message": "요청이 정상 처리되었습니다.",
  "data": {
    "qrState": "SCANNED",
    "signedIn": false,
    "signupRequired": false
  },
  "code": 200
}
```

## Error

인증 실패(401), 요청 검증 실패(400), 서버 에러(500)는 [공통 오류 형식](../../README.md#공통-오류-형식)이다.

## 예외

1. 인증 실패: 웹 세션 쿠키가 없거나 만료됐다. 401 (공통 오류 형식)
2. QR을 되돌리지 않음: `userCode`가 없거나, QR이 만료·종료됐거나, 이 세션 계정의 `VERIFIED` 상태가 아니다. 세션만 끝내고 200을 돌려준다
3. 요청 검증 실패: `userCode`가 64자를 넘는다. 400 (공통 오류 형식)
4. 서버 에러: 500 Internal Server Error (공통 오류 형식)
