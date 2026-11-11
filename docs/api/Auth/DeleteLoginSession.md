# 차량 로그아웃

`DELETE /api/v1/Auth/M/sign/DeleteLoginSession`

지금 access 토큰의 로그인 세션을 끝낸다. 그 뒤로 이 세션의 refresh 토큰은 `invalid_grant`로 거절된다.

## Header

- Authorization(Bearer jwt) : mandatory

## Request

없음. 세션은 access 토큰의 `sid` 클레임으로 찾는다.

## Response

data (boolean): 처리 결과. 성공하면 `true`

**\<Success>**

```json
{
  "message": "요청이 정상 처리되었습니다.",
  "data": true,
  "code": 200
}
```

## Error

이 API만의 오류 본문은 없다. 인증 실패(401)와 서버 에러(500)는 [공통 오류 형식](../README.md#공통-오류-형식)이다.

## 예외

1. 인증 실패: 토큰이 없거나 위조·만료됐다. 401, code 401
2. 이미 끝난 세션: 할 일이 없으므로 성공으로 본다. 200, code 200, `data` true
3. 서버 에러: 500 Internal Server Error (공통 오류 형식)

- 로그아웃한 뒤에도 그 access 토큰은 만료(최대 1시간)까지 `sign` 구간을 통과한다. 서버가 access 토큰을 검사할 때 DB를 보지 않기 때문이다. 앱은 로그아웃하면 access 토큰도 버린다.
