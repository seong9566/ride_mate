# 이 차에 로그인한 계정 목록

`GET /api/v1/Auth/M/GetLoginSessionList`

로그인 화면의 "이 차에 로그인한 계정" 목록을 준다. 기기 번호(`deviceId`)로 그 차의 살아 있는 차량 로그인 세션을 최근 사용 순으로 찾는다. 로그인 전에 부르므로 인증이 없다.

## Header

- Authorization(Bearer jwt) : 불필요

## Request

query다.

- deviceId (string): 앱이 처음 실행할 때 CSPRNG로 만든 UUID v4 설치 번호. 로그인할 때 보낸 `device_id`와 같은 값. 1~100자 — 필수

```text
?deviceId=3f2b8c1e-9a4d-4e7b-b1c2-7d9e0f6a5b43
```

## Response

data (array of objects): 계정 목록. 최근 사용 순(첫 항목이 가장 최근, 화면의 '최근 사용' 표시), 최대 10개. 없으면 빈 배열

- loginSessionSeq (integer): 로그인 세션 ID. 로그인 응답의 `session_id`와 같다. 앱이 보관한 계정별 refresh 토큰과 짝짓는다
- userName (string): 운전자 이름(엄마·아빠 등). 빈 문자열일 수 있다(앱은 대체 문구를 보여준다)

**\<Success>**

```json
{
  "message": "요청이 정상 처리되었습니다.",
  "data": [
    {
      "loginSessionSeq": 25,
      "userName": "엄마"
    },
    {
      "loginSessionSeq": 26,
      "userName": "아빠"
    }
  ],
  "code": 200
}
```

## Error

요청 검증 실패(400)와 서버 에러(500)는 [공통 오류 형식](../README.md#공통-오류-형식)이다. 요청 빈도 제한을 넘으면 본문 없이 HTTP 429이고 `Retry-After` 헤더에 창 길이(60초)가 들어간다. 남은 시간이 아니라 상한이라 이 시간이 지나면 반드시 풀린다.

## 예외

1. 요청 검증 실패: `deviceId`가 없거나 100자를 넘는다. 400 (공통 오류 형식)
2. 요청 빈도 제한: 같은 클라이언트(IPv4 주소, IPv6는 /64)에서 1분에 30번을 넘었다. 429 (본문 없음, `Retry-After`)
3. 서버 에러: 500 Internal Server Error (공통 오류 형식)

- 로그아웃했거나 만료(90일 동안 쓰지 않음)된 세션, 삭제된 계정은 목록에 나오지 않는다.
- 사용 시각·전화번호·계정 ID는 내보내지 않는다.
- `deviceId`는 로그인 전에 운전자 이름을 보는 열쇠다. 앱 저장소에만 두고 로그·분석 도구로 보내지 않는다(`backend/docs/security.md`).
- 계정을 고르면 앱이 그 계정의 refresh 토큰으로 [토큰 발급](Token.md)(`grant_type=refresh_token`)을 부른다. 앱에 refresh 토큰이 없거나 `invalid_grant`가 오면 QR 로그인으로 보낸다.
- 429는 `Retry-After` 초가 지난 뒤 다시 시도한다.
