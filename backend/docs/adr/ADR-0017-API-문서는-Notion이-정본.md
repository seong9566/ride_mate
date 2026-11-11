# ADR-0017 — API 문서는 Notion이 정본

- 상태: **채택** (2026-11-11)
- PR #12에서 정한 "API마다 레포 `docs/api/{도메인}/{액션}.md`를 둔다"를 대체한다. `openapi.json`은 그대로 레포에 둔다.

## 결정

- 앱↔서버 API 문서의 정본은 Notion "라이드메이트 API 문서"(https://app.notion.com/p/415aa638eaf880808d84c4ffba30bb51)다. 목차 페이지에 API 목록·공통 규칙·공통 오류 형식을 두고, API마다 하위 페이지(Header·Request·Response·Error·예외)를 둔다.
- 레포의 API별 문서(`docs/api/Auth/*.md`)와 양식 템플릿(`update-api-spec/templates/api-doc.md`)은 지운다. 양식 규칙은 `update-api-spec` 스킬 본문에 둔다.
- 필드 이름·타입의 기계 정본은 서버 Swagger 출력인 `docs/api/openapi.json`이다. 둘이 다르면 `openapi.json`이 맞고 Notion을 고친다.
- API를 바꾸는 PR은 `openapi.json`을 갱신하고 Notion 페이지를 고치며, PR 본문에 고친 페이지를 적는다.

## 근거

- 사용자 결정(2026-11-11): 모빌리티 앱 개발자가 보는 API 문서를 Notion에 둔다.
- 같은 내용을 레포와 Notion 두 곳에 두면 갱신할 때마다 둘을 맞춰야 하고, 하나를 빠뜨리면 어긋난다. 정본을 하나로 둔다.

## 결과

- API 문서 변경은 PR diff에 나오지 않는다. 리뷰어는 PR 본문의 Notion 페이지 목록으로 확인한다.
- Notion 갱신에는 Notion MCP 연결이 필요하다. 네트워크가 없는 실행 환경(codex 등)에는 맡기지 않는다.
- Notion 문서는 2026-11-11 시점 레포 문서(T08·T09·T10 머지 후)를 옮겨 만들었다.
