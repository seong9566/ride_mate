# 로그인 인증번호 발송(휴대폰 웹)

`POST /api/v1/Auth/W/AddPhoneVerification`

휴대폰 웹 로그인에 쓸 인증번호를 발송한다. 요청·응답과 재발송·잠금 규칙은 [차량용 인증번호 발송](../AddPhoneVerification.md)과 같다.

## Header

- Authorization(Bearer jwt) : 불필요
- Content-Type : `application/json`

## Request

JSON 본문이다.

- phone (string): 숫자만 쓴 국내 휴대폰 번호 — 필수

```json
{
  "phone": "01012345678"
}
```

## Response

data (object): 인증번호 유효 시간과 재발송 대기 시간

- expiresIn (integer): 인증번호 유효 시간(초)
- retryAfter (integer): 다시 발송할 수 있을 때까지 남은 시간(초)

**\<Success>**

```json
{
  "message": "요청이 정상 처리되었습니다.",
  "data": {
    "expiresIn": 180,
    "retryAfter": 60
  },
  "code": 200
}
```

## Error

요청 검증 실패(400)와 서버 에러(500)는 [공통 오류 형식](../../README.md#공통-오류-형식)이다. 재발송 대기와 잠금 응답은 [차량용 인증번호 발송](../AddPhoneVerification.md)을 따른다.

## 예외

1. 재발송 간격: 같은 번호로 1분 안에 다시 요청했다. HTTP 200, code 101
2. 잠금 중: 인증번호를 5번 틀려 10분 동안 잠겼다. HTTP 200, code 102
3. 요청 검증 실패: 번호가 국내 휴대폰 형식이 아니다. 400 (공통 오류 형식)
4. 서버 에러: 500 Internal Server Error (공통 오류 형식)
