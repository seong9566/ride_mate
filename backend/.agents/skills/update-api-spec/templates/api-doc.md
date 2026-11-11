<!--
API 문서 양식. 레포 루트 docs/api/{도메인}/{액션}.md로 복사해 쓴다.
- {…} 자리표시자를 모두 바꾸고, 이 주석을 포함한 HTML 주석을 모두 지운다. 자리표시자 안에는 큰따옴표를 쓰지 않는다(완료 조건의 grep이 놓친다).
- 절(Header, Request, Response, Error, 예외)의 이름과 순서를 바꾸지 않는다. 쓸 내용이 없는 절은 지우지 않고 "없음."이라고 쓴다.
- 타입은 JSON 타입으로 쓴다: string, integer, number, boolean, object, array of objects, array of strings.
- 필드 이름은 JSON에 나오는 그대로(대소문자 포함) 쓴다. 객체 안의 필드는 들여쓴 목록으로 쓴다.
- 예시 JSON의 형태·message 문구·code는 코드에서 실제로 나가는 그대로 쓴다. 토큰 같은 값은 형식만 맞춘 가짜 값을 쓴다.
- 요청 예시는 값의 위치에 맞춰 쓴다. JSON 본문은 json 펜스에 JSON으로, query는 text 펜스에 `?a=1&b=2`로, form은 text 펜스에 `a=1&b=2`로 쓴다.
- 401 본문과 400·500 ProblemDetails 같은 공통 오류 형식은 docs/api/README.md에 있다. 이 API만의 오류 본문이 없으면 Error 절에 README를 가리키는 한 줄만 쓴다.
- 예외 목록 아래에는 앱이 지켜야 할 점을 "- " 목록으로 덧붙일 수 있다.
-->
# {한글 설명}

`{HTTP 메서드} {경로}`

{이 API가 하는 일 한두 문장}

## Header

- Authorization(Bearer jwt) : {mandatory 또는 불필요}
- Content-Type : {요청 본문 형식. 본문이 없으면 이 줄을 지운다}

## Request

{값의 위치: JSON 본문, query, form 중 하나. 요청 값이 없으면 「없음.」으로 시작하고 아래 목록과 예시를 지운다}

- {필드} ({타입}): {설명} — {필수 또는 선택}

```{json 또는 text}
{요청 예시}
```

## Response

data ({타입}): {설명}

- {필드} ({타입}): {설명}

**\<Success>**

```json
{
  "message": "요청이 정상 처리되었습니다.",
  "data": {응답 예시},
  "code": 200
}
```

## Error

- {필드} ({타입}): {설명}

```json
{오류 예시}
```

## 예외

1. {상황}: {설명}. {HTTP 상태와 code}
2. 서버 에러: 500 Internal Server Error (공통 오류 형식)
