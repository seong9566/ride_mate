# 앱↔서버 API

| 무엇 | 어디 |
|---|---|
| API 문서(정본) — 로그인·회원가입 화면별 API 호출 순서, API별 Request·Response·Error·예외, 공통 오류 형식 | Notion "라이드메이트 API 문서": https://app.notion.com/p/415aa638eaf880808d84c4ffba30bb51 |
| `openapi.json` — 서버의 Swagger 출력 | 이 폴더. `backend/scripts/update-api-spec.sh`로만 갱신한다. 손으로 고치지 않는다 |

- 필드 이름과 타입이 Notion 문서와 `openapi.json`에서 다르면 `openapi.json`이 맞다. 다른 것을 발견하면 Notion 문서를 고친다.
- 갱신 절차는 `backend/.agents/skills/update-api-spec/SKILL.md`를 따른다.
