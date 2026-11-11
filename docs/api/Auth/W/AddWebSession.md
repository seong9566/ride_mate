# 인증번호 확인과 가입(휴대폰 웹)

`POST /api/v1/Auth/W/AddWebSession`

인증번호를 확인하고 웹 세션을 만든다. 처음 온 번호는 이름과 필수 약관 동의까지 받아 계정과 동의 기록을 함께 만든다.

## Header

- Authorization(Bearer jwt) : 불필요
- Content-Type : `application/json`

## Request

JSON 본문이다.

- phone (string): 숫자로만 된 국내 휴대폰 번호 — 필수
- code (string): 숫자 6자리 인증번호 — 필수
- userCode (string): QR 주소에 담긴 코드. 1~64자 — 필수
- userName (string): 처음 가입할 운전자 이름. 앞뒤 공백 제외 1~50 유니코드 문자 — 선택
- agreeTerms (boolean): 서비스 이용약관 동의 — 선택, 처음 가입할 때 `true` 필수
- agreePrivacy (boolean): 개인정보 수집·이용 동의 — 선택, 처음 가입할 때 `true` 필수

```json
{
  "phone": "01012345678",
  "code": "482173",
  "userCode": "AAAAAAAAAAAAAAAAAAAAAA",
  "userName": "엄마",
  "agreeTerms": true,
  "agreePrivacy": true
}
```

## Response

data (object): 웹 로그인과 QR 본인 확인 결과

- qrState (string): `VERIFIED`
- signedIn (boolean): `true`
- signupRequired (boolean): `false`
- userName (string): 운전자 이름
- maskedPhone (string): `010-****-5678` 형식의 가린 번호
- matchCode (string): 차와 비교할 숫자 4자리
- expiresIn (integer): QR 만료까지 남은 초

성공하면 응답의 `Set-Cookie`에 `rm_web_session`이 설정된다. 세션 토큰은 JSON에 들어가지 않는다.

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
    "expiresIn": 118
  },
  "code": 200
}
```

## Error

- error (string): `invalid_request`, `otp_not_requested`, `otp_locked`, `otp_expired`, `otp_mismatch`, `terms_required`
- remainingAttempts (integer): 인증번호가 틀렸을 때 남은 횟수
- retryAfter (integer): 잠금이 풀릴 때까지 남은 초

```json
{
  "message": "잘못된 요청입니다.",
  "data": {
    "signedIn": false,
    "signupRequired": false,
    "error": "otp_mismatch",
    "remainingAttempts": 4
  },
  "code": 400
}
```

## 예외

1. 가입 정보 필요: 처음 온 번호인데 이름이 비었다. 인증번호를 사용 처리하지 않는다. HTTP 200, code 101, `signupRequired` true
2. 약관 동의 필요: 처음 가입하면서 필수 약관 둘 모두에 동의하지 않았다. 400, code 400, `error` `terms_required`
3. 인증번호 없음·사용됨: 400, code 400, `error` `otp_not_requested`
4. 인증번호 잠김·만료·불일치: 400, code 400, 각각 `otp_locked`·`otp_expired`·`otp_mismatch`
5. QR 없음·종료·취소·만료: 404 또는 409이며 `qrState`로 구분한다. 인증번호는 사용 처리하지 않는다
6. 요청 검증 실패: 필수 값·형식·길이가 맞지 않는다. 400 (공통 오류 형식)
7. 서버 에러: 500 Internal Server Error (공통 오류 형식)

- code 101을 받으면 같은 `phone`·`code`·`userCode`에 이름과 두 약관 동의를 붙여 다시 보낸다.
