---
name: update-api-spec
description: RideMate 백엔드의 라우트·DTO를 바꾼 뒤 앱↔서버 API 명세(루트 docs/api/openapi.json)를 서버의 Swagger 출력으로 갱신할 때 쓴다. "API 명세 갱신", "openapi 갱신", "swagger 내보내기"에 쓴다.
---

# update-api-spec

결과 파일은 레포 루트의 `docs/api/openapi.json` 하나다. 갱신은 `backend/scripts/update-api-spec.sh`가 한다. 이 스크립트가 빌드, 임시 서버 기동(5168 포트, 최대 30초 대기), 명세 저장, 서버 종료를 한 번에 하고, 받은 명세가 온전한 JSON일 때만 기존 파일을 바꾼다.

## 하지 말 것

- `docs/api/openapi.json`을 손으로 고치지 않는다. 항상 스크립트로 덮어쓴다.
- 서버 기동·curl·종료를 손으로 따로 하지 않는다. 스크립트를 쓴다.
- 5168 포트를 다른 프로세스가 쓰고 있으면 그 프로세스를 종료하지 않는다. `UPDATE_API_SPEC_PORT=<빈 포트>`를 앞에 붙여 다시 실행한다. 개발 서버(5167)는 떠 있어도 된다.

## 절차

1. `backend/`에서 `sh scripts/update-api-spec.sh`를 실행한다.
2. 종료 코드가 0이 아니면 출력된 사유(포트 사용 중, 빌드 실패, 기동 실패)를 고치거나 사용자에게 알린다. 실패하면 기존 명세는 바뀌지 않는다.
3. 변경한 코드와 같은 커밋 또는 같은 PR에 명세를 넣는다.

## 완료 조건

- [ ] `sh scripts/update-api-spec.sh`가 종료 코드 0으로 끝났다.
- [ ] `git status --short -- ../docs/api/`에 `openapi.json`이 보이고, `git diff -- ../docs/api/`에 의도한 라우트·DTO 변경만 있다(처음 생긴 파일은 diff에 나오지 않으니 내용을 직접 본다).
- [ ] `lsof -ti :5168`의 출력이 없다(임시 서버가 종료됐다).
- [ ] 명세가 코드 변경과 같은 커밋 또는 같은 PR에 들어 있다.
