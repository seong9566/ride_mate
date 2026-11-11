# 앱↔서버 API 문서

| 파일 | 내용 | 만드는 방법 |
|---|---|---|
| `openapi.json` | 서버의 Swagger 출력 | `backend/scripts/update-api-spec.sh`로만 갱신한다. 손으로 고치지 않는다 |
| `{도메인}/{액션}.md` | API 하나의 Header·Request·Response·Error·예외 | 양식 `backend/.agents/skills/update-api-spec/templates/api-doc.md`로 쓴다 |

- 필드 이름과 타입이 두 파일에서 다르면 `openapi.json`이 맞다. 코드에서 바로 뽑기 때문이다.
- 응답 `code` 값과 HTTP 상태의 대응은 `backend/docs/api.md` §응답을 따른다.

## API 목록

| 도메인 | API | 인증 | 문서 |
|---|---|---|---|
| Auth | `POST /api/v1/Auth/M/Token` 토큰 발급 | 불필요 | [Token](Auth/Token.md) |
| Auth | `DELETE /api/v1/Auth/M/sign/DeleteLoginSession` 차량 로그아웃 | Bearer | [DeleteLoginSession](Auth/DeleteLoginSession.md) |

## 공통 오류 형식

### 인증 실패 (401)

경로에 `sign` 구간이 있는 API에 토큰이 없거나 위조·만료된 토큰을 보내면 아래 본문이 나간다.

```json
{
  "message": "잘못된 요청입니다.",
  "data": null,
  "code": 401
}
```

### 요청 검증 실패 (400)

요청 값(JSON 본문, query)이 DTO 검증 규칙에 맞지 않거나 JSON 형식이 깨졌으면 ASP.NET Core의 `ProblemDetails`가 나간다. `errors`에 필드별 메시지가 들어간다.

- status (integer): 400
- errors (object): 필드 이름을 키로, 메시지 목록(array of strings)을 값으로 갖는다

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "phone": [ "The phone field is required." ]
  },
  "traceId": "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-00"
}
```

### 서버 에러 (500)

Controller가 잡은 예외와 Service가 돌려준 500은 `ProblemDetails`로 나간다. 그 밖(미들웨어 등)에서 난 오류는 본문 형식이 다를 수 있으므로 앱은 본문이 아니라 HTTP 상태로 판단한다. 사용자에게 보여줄 문구는 `detail`에 있다.

- status (integer): 500
- detail (string): 오류 설명

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.6.1",
  "title": "An error occurred while processing your request.",
  "status": 500,
  "detail": "서버에서 요청을 처리하지 못하였습니다. 잠시후 다시 시도해주세요.",
  "traceId": "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-00"
}
```
