---
name: update-api-spec
description: RideMate 백엔드의 라우트·DTO나 응답 code·message·오류 상황을 바꾼 뒤 앱↔서버 API 명세(루트 docs/api/의 openapi.json과 API별 문서)를 갱신할 때 쓴다. "API 명세 갱신", "openapi 갱신", "swagger 내보내기", "API 문서 작성"에 쓴다.
---

# update-api-spec

결과물은 레포 루트 `docs/api/`의 두 가지다.

| 파일 | 만드는 방법 |
|---|---|
| `openapi.json` | `backend/scripts/update-api-spec.sh`가 서버의 Swagger 출력으로 덮어쓴다. 이 스크립트가 빌드, 임시 서버 기동(5168 포트, 최대 30초 대기), 명세 저장, 서버 종료를 한 번에 하고, 받은 명세가 온전한 JSON일 때만 기존 파일을 바꾼다 |
| `{도메인}/{액션}.md`, `README.md`의 API 목록 | 이 스킬 폴더의 `templates/api-doc.md` 양식으로 손으로 쓴다. API 하나에 파일 하나다(예: `Auth/Token.md`) |

## 하지 말 것

- `docs/api/openapi.json`을 손으로 고치지 않는다. 항상 스크립트로 덮어쓴다.
- 서버 기동·curl·종료를 손으로 따로 하지 않는다. 스크립트를 쓴다.
- 5168 포트를 다른 프로세스가 쓰고 있으면 그 프로세스를 종료하지 않는다. `UPDATE_API_SPEC_PORT=<빈 포트>`를 앞에 붙여 다시 실행한다. 개발 서버(5167)는 떠 있어도 된다.
- API 문서의 절(Header, Request, Response, Error, 예외)의 이름과 순서를 바꾸거나 빼지 않는다. 쓸 내용이 없으면 "없음."이라고 쓴다. 단, Error 절은 이 API만의 오류 본문이 없으면 `docs/api/README.md`의 공통 오류 형식을 가리키는 한 줄을 쓴다.
- API 문서를 짐작으로 쓰지 않는다. 아래 "문서에 쓸 값의 출처"에서 확인한 값만 쓴다.

## 문서에 쓸 값의 출처

| 문서 항목 | 확인할 곳 |
|---|---|
| 경로, HTTP 메서드, Content-Type | Controller의 `[Route]`·`[Http…]`·`[Consumes]` |
| Authorization | 경로에 `sign` 구간이 있으면 mandatory, 없으면 불필요(`backend/docs/security.md`) |
| 요청 값의 위치(JSON 본문, query, form) | Controller 매개변수의 `[FromBody]`·`[FromQuery]`·`[FromForm]` |
| 필드 이름, 타입 | 요청·응답 DTO와 갱신한 `openapi.json` |
| 필수 여부 | DTO의 검증 특성(`[Required]` 등). 특성이 없으면 Service의 입력 검증 |
| `message`, `code`, 예외 상황 | Service의 `return` 문 |
| 오류 본문 | Controller의 `[ProducesResponseType]`과 `ApiControllerBase` |

## 절차

1. `backend/`에서 `sh scripts/update-api-spec.sh`를 실행한다.
2. 종료 코드가 0이 아니면 출력된 사유(포트 사용 중, 빌드 실패, 기동 실패)를 고치거나 사용자에게 알린다. 실패하면 기존 명세는 바뀌지 않는다.
3. 추가하거나 바꾼 API마다 `docs/api/{도메인}/{액션}.md`를 쓴다.
   - 새 API면 `templates/api-doc.md`를 복사해 자리표시자를 채우고 HTML 주석을 지운다.
   - 기존 API면 바뀐 절만 고친다.
   - 지운 API는 문서도 지운다.
4. `docs/api/README.md`의 API 목록 표를 `openapi.json`의 경로와 맞춘다.
5. 변경한 코드와 같은 커밋 또는 같은 PR에 명세와 문서를 넣는다.

## 완료 조건

- [ ] `sh scripts/update-api-spec.sh`가 종료 코드 0으로 끝났다.
- [ ] 라우트·DTO를 바꿨으면 커밋하기 전에 `git status --short -- ../docs/api/`에 `openapi.json`이 보이고, `git diff -- ../docs/api/openapi.json`에 의도한 라우트·DTO 변경만 있다(처음 생긴 파일은 diff에 나오지 않으니 내용을 직접 본다). 라우트·DTO를 바꾸지 않았으면 `openapi.json`의 diff가 없다.
- [ ] `lsof -ti :5168`의 출력이 없다(임시 서버가 종료됐다).
- [ ] 바꾼 API마다 `docs/api/{도메인}/{액션}.md`가 있고, `README.md` 목록의 경로가 `openapi.json`의 경로와 같다.
- [ ] API 문서에 자리표시자와 양식 주석이 남지 않았다. `grep -rnE '\{[^}"]*[가-힣][^}"]*\}|<!--' ../docs/api/*/`의 출력이 없다.
- [ ] 명세와 문서가 코드 변경과 같은 커밋 또는 같은 PR에 들어 있다.
