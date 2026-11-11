---
name: update-api-spec
description: RideMate 백엔드의 라우트·DTO나 응답 code·message·오류 상황을 바꾼 뒤 앱↔서버 API 명세(루트 docs/api/openapi.json)와 Notion API 문서(정본)를 갱신할 때 쓴다. "API 명세 갱신", "openapi 갱신", "swagger 내보내기", "API 문서 작성"에 쓴다.
---

# update-api-spec

결과물은 두 가지다.

| 결과물 | 만드는 방법 |
|---|---|
| 레포 루트 `docs/api/openapi.json` | `backend/scripts/update-api-spec.sh`가 서버의 Swagger 출력으로 덮어쓴다. 이 스크립트가 빌드, 임시 서버 기동(5168 포트, 최대 30초 대기), 명세 저장, 서버 종료를 한 번에 하고, 받은 명세가 온전한 JSON일 때만 기존 파일을 바꾼다 |
| Notion "라이드메이트 API 문서" — **정본** | Notion MCP(`notion-fetch`·`notion-update-page`·`notion-create-pages`)로 쓴다. 목차 페이지 https://app.notion.com/p/415aa638eaf880808d84c4ffba30bb51 에 API 목록 표 두 개(`차량 앱 (/M/)`, `휴대폰 웹 (/W/)`)와 공통 규칙·공통 오류 형식이 있고, API 하나에 하위 페이지 하나다 |

## 하지 말 것

- `docs/api/openapi.json`을 손으로 고치지 않는다. 항상 스크립트로 덮어쓴다.
- 서버 기동·curl·종료를 손으로 따로 하지 않는다. 스크립트를 쓴다.
- 5168 포트를 다른 프로세스가 쓰고 있으면 그 프로세스를 종료하지 않는다. `UPDATE_API_SPEC_PORT=<빈 포트>`를 앞에 붙여 다시 실행한다. 개발 서버(5167)는 떠 있어도 된다.
- 레포에 API별 문서(`docs/api/{도메인}/{액션}.md`)를 다시 만들지 않는다. API 문서는 Notion에만 둔다.
- Notion에 연결되지 않은 실행 환경(codex 등)에서는 절차 1~2(`openapi.json`)만 하고 3~6을 하지 않는다. 새로 쓰거나 고칠 Notion 페이지와 바뀐 내용을 결과 보고에 남긴다. Notion은 연결된 세션이 쓴다.
- Notion 마크다운 문법을 짐작하지 않는다. 처음 쓰기 전에 `notion://docs/notion-flavored-markdown-spec`을 `notion-fetch`로 읽고, 표·멘션은 목차 페이지의 기존 행과 같은 형식으로 쓴다.
- API 페이지의 절(Header, Request, Response, Error, 예외)의 이름과 순서를 바꾸거나 빼지 않는다. 쓸 내용이 없으면 "없음."이라고 쓴다. Error 절은 이 API만의 오류 본문이 없으면 목차 페이지의 공통 오류 형식을 가리키는 한 줄을 쓴다.
- API 문서를 짐작으로 쓰지 않는다. 아래 "문서에 쓸 값의 출처"에서 확인한 값만 쓴다.
- 기존 페이지는 `update_content`로 바뀐 부분만 고친다. 페이지 전체를 다시 쓰지 않는다. 페이지를 지우는 것은 사용자에게 확인을 받은 뒤에만 한다.

## 문서에 쓸 값의 출처

| 문서 항목 | 확인할 곳 |
|---|---|
| 경로, HTTP 메서드, Content-Type | Controller의 `[Route]`·`[Http…]`·`[Consumes]` |
| Authorization | 경로에 `M/sign` 구간이 있으면 Bearer, `W/sign` 구간이면 웹 세션 쿠키, 없으면 불필요(`backend/docs/security.md`) |
| 요청 값의 위치(JSON 본문, query, form) | Controller 매개변수의 `[FromBody]`·`[FromQuery]`·`[FromForm]` |
| 필드 이름, 타입 | 요청·응답 DTO와 갱신한 `openapi.json` |
| 필수 여부 | DTO나 Controller 매개변수의 검증 특성(`[Required]` 등). 특성이 없으면 Service의 입력 검증 |
| `message`, `code`, 예외 상황 | Service의 `return` 문 |
| 오류 본문 | Controller의 `[ProducesResponseType]`과 `ApiControllerBase` |

## API 페이지 양식

- 제목은 `notion-create-pages`의 `properties.title`에 넣고 본문에는 쓰지 않는다. `/M/` API는 `[차량] {한글 기능 이름}`, `/W/` API는 `[웹] {한글 기능 이름}`이다(예: `[차량] 토큰 발급`).
- 본문 순서: 첫 줄에 `` `{HTTP 메서드} {경로}` ``, 한두 문장 설명, 그다음 아래 다섯 절.

| 절 | 쓰는 것 |
|---|---|
| Header | `- Authorization(Bearer jwt) : mandatory` / `불필요` / `` 쿠키(`__Host-rm_web_session`) ``(`W/sign`), `- Content-Type : …`(본문이 없으면 뺀다) |
| Request | 값의 위치(JSON 본문·query·form), 필드 목록 `- 필드 (타입): 설명 — 필수/선택`, 요청 예시(JSON은 json 펜스, query는 `?a=1`, form은 `a=1&b=2`를 text 펜스) |
| Response | `data (타입): 설명`, 필드 목록, `**\<Success>**` 아래 실제 응답 예시 |
| Error | 이 API만의 오류 필드와 예시. 없으면 공통 오류 형식을 가리키는 한 줄 |
| 예외 | `1. 상황: 설명. HTTP 상태와 code` 목록. 마지막 항목은 항상 `서버 에러: 500 Internal Server Error (공통 오류 형식)`. 그 아래에 앱이 지킬 점을 `- ` 목록으로 덧붙일 수 있다 |

- 타입은 JSON 타입(string, integer, number, boolean, object, array of objects, array of strings)으로 쓴다. 필드 이름은 JSON에 나오는 그대로(대소문자 포함) 쓰고, 객체 안의 필드는 들여쓴 목록으로 쓴다.
- 예시 JSON의 형태·`message`·`code`는 코드에서 실제로 나가는 그대로 쓴다. 토큰 같은 값은 형식만 맞춘 가짜 값을 쓴다.
- 다른 API를 가리킬 때는 그 페이지의 멘션(`<mention url="{페이지 URL}">{제목}</mention>`)을 쓴다.

## 절차

API 문서는 PR 단계에서 고친다. 머지 전에 바뀐 내용이 앱 개발자에게 보이므로 "머지 대기" 표시를 한다.

1. `backend/`에서 `sh scripts/update-api-spec.sh`를 실행한다.
2. 종료 코드가 0이 아니면 출력된 사유(포트 사용 중, 빌드 실패, 기동 실패)를 고치거나 사용자에게 알린다. 실패하면 기존 명세는 바뀌지 않는다.
3. Notion 목차 페이지를 `notion-fetch`로 읽는다. 기존 API 페이지는 목록 표 `문서` 칸의 멘션 URL로 찾는다.
4. 추가하거나 바꾼 API마다 Notion 페이지를 고친다.
   - 새 API: 목차 페이지 아래에 하위 페이지를 만들고(`notion-create-pages`, parent = 목차 페이지) 위 양식으로 쓴다. 클라이언트에 맞는 목록 표(`차량 앱 (/M/)` 또는 `휴대폰 웹 (/W/)`)의 마지막 행 뒤에 `| `{메서드} {경로}` | {기능} | {인증} | {멘션} |` 행을 `update_content`로 넣는다. 열은 `API | 기능 | 인증 | 문서`다.
   - 기존 API: 그 페이지를 읽고 바뀐 절만 `update_content`로 고친다.
   - 지운 API: 사용자에게 확인을 받은 뒤 페이지를 지우고 목록 표에서 행을 뺀다.
   - 고친 페이지 맨 위에 `<callout icon="⏳" color="yellow_bg">머지 대기 — PR #{번호}. 아직 서버에 반영되지 않은 내용이다.</callout>`을 넣는다(PR 번호를 모르면 PR을 만든 뒤 넣는다).
5. 공통 규칙(주소·응답 형식·공통 오류)이 바뀌었으면 목차 페이지의 해당 절도 고치고 같은 표시를 한다.
6. PR을 만들 때 본문에 고친 Notion 페이지 제목과 바뀐 내용 요약을 적는다.
7. 머지한 뒤 그 페이지들의 "머지 대기" 표시를 지운다. 머지하지 않고 PR을 닫으면 Notion 변경을 되돌린다(새 페이지는 사용자 확인 뒤 지우고, 고친 절은 PR 전 내용으로 되돌리고, 목록 표의 행을 뺀다).

## 완료 조건

- [ ] `sh scripts/update-api-spec.sh`가 종료 코드 0으로 끝났다.
- [ ] 라우트·DTO·Controller 특성(`[Consumes]`·`[ProducesResponseType]`·`[SwaggerOperation]` 등)을 바꿨으면 커밋하기 전에 `git status --short -- ../docs/api/`에 `openapi.json`이 보이고, `git diff -- ../docs/api/openapi.json`에 의도한 변경만 있다. 셋 다 바꾸지 않았으면 `openapi.json`의 diff가 없다.
- [ ] `lsof -ti :5168`의 출력이 없다(임시 서버가 종료됐다).
- [ ] 목차 목록 표의 `{메서드} {경로}` 목록이 아래 명령의 출력과 빠짐·남음 없이 같다(`notion-fetch`로 목차를 읽어 비교한다).
      `jq -r '.paths | to_entries[] | .key as $p | .value | keys[] | ascii_upcase + " " + $p' ../docs/api/openapi.json | sort`
- [ ] 고친 Notion 페이지에 "머지 대기" 표시가 있고, 그 목록을 PR 본문(또는 Notion에 연결되지 않은 환경이면 결과 보고)에 적었다.
