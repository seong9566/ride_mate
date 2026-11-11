# 로그인 인증번호 발송

`POST /api/v1/Auth/M/AddPhoneVerification`

휴대폰 번호로 로그인 인증번호(6자리, 3분 유효)를 보낸다. 받은 인증번호는 [토큰 발급](Token.md)의 `grant_type=otp`로 확인한다. 아직 가입하지 않은 번호에도 보낸다(가입은 휴대폰 웹).

## Header

- Authorization(Bearer jwt) : 불필요
- Content-Type : `application/json`

## Request

JSON 본문이다.

- phone (string): 숫자만 쓴 휴대폰 번호. 형식 `^01[016789][0-9]{7,8}$` — 필수

```json
{
  "phone": "01012345678"
}
```

## Response

data (object): 인증번호 유효 시간과 다시 받을 수 있을 때까지의 시간

- expiresIn (integer): 인증번호 유효 시간(초). 보냈으면 180, 보내지 않았으면 0
- retryAfter (integer): 다시 요청할 수 있을 때까지 남은 시간(초)

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

요청 검증 실패(400)와 서버 에러(500)는 [공통 오류 형식](../README.md#공통-오류-형식)이다. 보내지 않은 경우(101·102)는 HTTP 200이고 형태는 아래와 같다.

- data.expiresIn (integer): 0
- data.retryAfter (integer): 다시 요청할 수 있을 때까지 남은 시간(초)

```json
{
  "message": "잘못된 요청입니다.",
  "data": {
    "expiresIn": 0,
    "retryAfter": 42
  },
  "code": 101
}
```

## 예외

1. 재발송 간격: 같은 번호로 1분 안에 다시 요청했다. 보내지 않는다. HTTP 200, code 101
2. 잠금 중: 인증번호를 5번 틀려 10분 동안 잠겼다. 보내지 않는다. HTTP 200, code 102
3. 번호 형식 오류·값 없음: 요청 검증 실패. HTTP 400 (공통 오류 형식)
4. 서버 에러: 500 Internal Server Error (공통 오류 형식)

- 다시 받으면 이전 인증번호는 쓸 수 없다. 가장 최근에 받은 인증번호만 확인한다.
- 틀린 횟수는 번호 단위로 센다. 잠기지 않았고 10분이 지나지 않았으면 다시 받아도 틀린 횟수가 이어진다.
- 처음 보는 번호로 동시에 여러 번 요청해도 한 번만 보낸다. 나머지는 101이다. 드물게 500이 나면 다시 요청하면 101이 온다.
