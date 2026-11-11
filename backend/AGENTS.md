# RideMate 백엔드 — 에이전트 작업 지침

- Claude Code와 Codex 세션은 `backend/`에서 시작한다. 레포 루트에서 시작한 Codex는 `backend/.agents/skills/`의 스킬을 찾지 못한다.
- `backend/` 작업은 이 문서를 따른다. `~/AGENTS.md` 등 상위 지침과 충돌하면 이 문서가 우선한다. [ADR-0001]
- `backend/` 밖의 파일은 수정하지 않는다. 예외는 앱↔서버 API 명세인 루트 `docs/api/`뿐이다. 그 밖이 필요하면 수정하지 말고 사용자에게 알린다.
- 기획 판단이 필요하면 PM vault의 PRD를 먼저 읽는다: `personal_dev/wiki/projects/ride-mate/prd.md`
- PM vault 경로는 vault 폴더(`personal_dev`)부터 적는다. vault가 놓인 위치는 컴퓨터마다 다르므로 절대 경로를 문서에 쓰지 않는다. 실제 위치는 환경변수 `TASK_VAULT`가 가리킨다(`personal_dev/…` = `$TASK_VAULT/…`). `TASK_VAULT`가 비어 있으면 멈추고 사용자에게 vault 위치를 묻는다.

## 작업 전에 읽을 문서

| 작업 | 문서 |
|---|---|
| 폴더 구조·계층 방향·Program.cs 순서·DI 등록 | `docs/architecture/overview.md` |
| Controller | `docs/architecture/controller.md` |
| Service | `docs/architecture/service.md` |
| Repository·쿼리(EF Core / Dapper) | `docs/architecture/repository.md` |
| DTO | `docs/architecture/dto.md` |
| 테이블·스키마·DbContext·로컬 DB | `docs/architecture/database.md` |
| 테이블 목록·컬럼·코드 값 | `docs/architecture/schema.md` |
| 라우트·응답 code·커서 페이징·API 명세 | `docs/api.md` |
| 설정·비밀값·로그·입력 처리·인증 | `docs/security.md` |
| 네이밍·코드 스타일 | `docs/coding-style.md` |
| 규칙을 그렇게 정한 이유(결정 기록) | `docs/adr/README.md` |
| 기능 요구사항(화면 동작·입력 검증·데이터·상태 문구) | `docs/spec/README.md` → 해당 시트 |

## 스킬

| 스킬 | 쓰는 때 |
|---|---|
| `start-task` | 작업을 시작할 때 — 카드 확인·생성, wip 전환, 브랜치 생성 |
| `add-domain` | 도메인이나 API를 추가할 때 |
| `db-schema-change` | 테이블을 만들거나 바꿀 때 |
| `update-api-spec` | 라우트·DTO나 응답 code·message·오류 상황을 바꾼 뒤 `docs/api/openapi.json`과 Notion API 문서(정본)를 갱신할 때 |

- 스킬 정본은 `.agents/skills/<이름>/SKILL.md`다. `.claude/skills/<이름>`은 정본을 가리키는 심볼릭 링크이므로 스킬은 `.agents/skills/`에서만 고친다.
- 코드 템플릿은 스킬 폴더의 `templates/`에만 둔다. 문서에는 규칙과 템플릿 경로만 쓴다. 레이어 규칙의 정본은 `docs/`다. [ADR-0007]
- 스킬을 추가하면 `backend/`에서 같은 이름으로 링크를 만든다: `ln -s ../../.agents/skills/<이름> .claude/skills/<이름>`

## 핵심 규칙

- 솔루션은 `RideMateService/RideMateService.sln`, 앱 프로젝트는 `RideMateService/RideMateService/`다.
- 의존 방향은 Controller → Service → Repository 하나뿐이다. 역참조·레이어 건너뛰기를 하지 않는다. [ADR-0001]
- Service는 항상 `ResponseModel<T>`를 반환한다. 예외를 던지지 않고 `code = 500`으로 바꾼다(요청 취소만 그대로 올려보낸다). [ADR-0004]
- Repository는 DTO를 받지 않는다. Dapper 조회 결과를 응답 DTO로 바로 매핑하는 것만 허용한다.
- GET API의 조회에는 EF Core를 쓰지 않고 Dapper로 SQL을 직접 쓴다(쿼리 튜닝과 속도). EF Core는 등록·수정·삭제에만 쓴다. [ADR-0003]
- 스키마는 DB-first다. EF Migration을 쓰지 않고, 스캐폴드 산출물(`Commons/DBModels`, `RideMateContext.cs`)을 직접 고치지 않는다. [ADR-0002]
- 비밀값을 코드나 `appsettings.json`에 쓰지 않는다.

## 빌드·실행·검증

명령은 `backend/RideMateService/`에서 실행한다.

- 빌드: `dotnet build RideMateService.sln -warnaserror`
- 실행: `dotnet run --project RideMateService --launch-profile http` → `http://localhost:5167/swagger`
- 서버는 DB 연결 문자열이 없으면 뜨지 않는다. 새로 클론했으면 `db-schema-change` 스킬의 2단계(로컬 MySQL 준비)를 먼저 한다.
- 작업을 끝내기 전에 빌드가 경고 0으로 통과해야 한다.
- 엔드포인트·DTO를 바꿨으면 서버를 띄워 Swagger에서 그 API를 호출해 응답을 확인하고, `update-api-spec` 스킬로 `openapi.json`을 같은 PR에 넣고 Notion API 문서(정본)를 고친다. 응답 `code`·`message`·오류 상황만 바꿨어도 Notion API 문서를 고친다.
- 확인 호출은 로컬 서버(`localhost`)와 로컬 Docker DB를 대상으로 할 때만 POST·PATCH·DELETE를 승인 없이 쓴다. 이 경우에 한해 전역 규칙 "검증 호출은 GET만"보다 이 문서가 우선한다. 배포된 서버나 원격 DB에는 GET만 쓰고, 쓰기가 필요하면 사용자 승인을 받는다.
- 서버를 띄우기 전에 `lsof -ti :5167`이 비어 있는지 본다. 이미 쓰이고 있으면 그 프로세스를 종료하지 않는다. `dotnet run --project RideMateService --urls http://localhost:<빈 포트>`로 띄우거나 사용자에게 알린다.
- 자신이 띄운 서버만, 띄울 때 기록한 PID로 종료한다. 포트로 찾은 PID나 `pkill`처럼 이름으로 종료하지 않는다.
- 테스트 프로젝트는 두지 않는다. [ADR-0006]

## 작업 흐름

- 백엔드 작업의 통합 브랜치는 `backend`다. 작업마다 `backend`에서 브랜치를 만들고 `backend`를 base로 PR을 연다(`gh pr create --base backend`). 워크트리도 `backend`에서 판다. 시작은 `start-task` 스킬로 한다.
- `main`과 `backend`에 직접 커밋하지 않는다.
- `backend` → `main` 반영은 사용자가 지시할 때만 한다. `backend`를 head, `main`을 base로 PR을 열고 머지 커밋으로 머지한다: `merge-pr.sh <PR#> --method merge`(스쿼시하면 두 브랜치의 SHA가 갈라져 다음 반영 PR이 거짓 충돌을 낸다).
- 기능(feat)·버그(fix) 작업은 PM vault 작업 카드 단위로 한다. 카드 위치: `personal_dev/wiki/projects/ride-mate/tasks/`
  - 카드가 없으면 먼저 만든다. 빈 번호를 `ls`로 확인한다. 쓰인 번호를 재사용하지 않는다.
  - 브랜치 이름에 카드 ID를 넣는다: `feat/T01-<설명>`, `fix/T02-<설명>`.
  - 착수할 때 `wip`, 머지한 뒤 `done`으로 바꾼다.
- Notion API 문서를 고친 PR은 머지한 뒤 그 페이지들의 "머지 대기" 표시를 지운다. 머지하지 않고 닫으면 Notion 변경을 되돌린다(`update-api-spec` 스킬).
- 설정·문서 같은 잡일은 카드 없이 `chore/<설명>`·`docs/<설명>` 브랜치로 한다.

```
bash "$TASK_VAULT/.claude/scripts/task.sh" new  T0x "<제목>" --project ride-mate --section code
bash "$TASK_VAULT/.claude/scripts/task.sh" wip  T0x --project ride-mate --section code
bash "$TASK_VAULT/.claude/scripts/task.sh" done T0x --project ride-mate
```

- 커밋 메시지는 `<type>: <한국어 요약>` 형식이다. type은 feat·fix·refactor·docs·chore 중 하나다.
- pre-commit 훅을 우회하지 않는다(`--no-verify` 금지).

## PM 세션 핸드오프 (작업 완료 시 필수)

작업·하위작업 완료나 PM에게 넘길 일이 생기면 vault handoff에 한 줄 남긴다 — 손으로 쓰지 말고 스크립트로:
`bash "$TASK_VAULT/.claude/scripts/handoff.sh" done ride-mate "<설명>" [--closes <ID>] [--pr <번호>]`

- 슬러그는 정확히 `ride-mate`. 완료 아닌 메모는 `note`, PM이 할 일로 가져갈 건 `todo`. 실기기·테스트 버그는 `--bug`.
- 하네스(AGENTS.md·hooks·skills 등) 개선점은 `todo ride-mate "[하네스] <개선점> — <파일/커밋>"`(아이디어면 `note`), 고칠 방법도 함께.
- 해소한 작업 카드는 `--closes <ID>`로 인용한다. 카드를 닫는 시점과 명령은 "작업 흐름" 절에 적힌 대로(머지 뒤, `--project ride-mate`) 한다.
- 작업일과 다른 날 남길 때만 설명 끝에 `(완료 YYYY-MM-DD)`를 붙인다. `(반영 …)`은 PM 전용이라 쓰지 않는다. 규약 정본: `personal_dev/inbox/HANDOFF_GUIDE.md`.
- `/pr-finish`를 돌릴 때 스킬에 적힌 `handoff.sh`·`task.sh` 호출은 모두 `$TASK_VAULT/.claude/scripts/`의 스크립트로 바꿔 하고, `task.sh`에는 `--project ride-mate`를 붙인다.
  - 2단계(handoff): 위 스크립트에 `--pr <번호> --status "머지 대기"`를 붙여 실행한다. 카드가 있으면 `wip`로 둔다.
  - 머지 뒤 카드 닫기: `bash "$TASK_VAULT/.claude/scripts/task.sh" done <ID> --project ride-mate`.
  - Notion API 문서를 고친 PR: PR을 만든 직후 고친 페이지의 "머지 대기" 표시에 PR 번호를 채우고 PR 본문에 페이지 목록을 적는다. 머지 뒤에는 카드 닫기와 함께 `update-api-spec` 스킬 7단계(표시 제거, "머지 시 삭제" 페이지 삭제)를 한다.
  - 카드의 완료기준 일부만 끝낸 PR: handoff에 `--closes`를 붙이지 않고, 머지 뒤 `bash "$TASK_VAULT/.claude/scripts/task.sh" todo <ID> --project ride-mate`로 되돌린다.

## 커밋 훅

- 클론한 뒤 한 번 설치한다: `sh scripts/install-hooks.sh` (`backend/`에서 실행)
- `git config core.hooksPath`는 바꾸지 않는다.
- 훅을 고치면 `sh githooks/tests/test-pre-commit.sh`가 `실패: 0`으로 끝나는지 확인한다. 테스트는 임시 복제본에서만 돈다.
