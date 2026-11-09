# RideMate 백엔드 — 에이전트 작업 지침

- Claude Code와 Codex 세션은 `backend/`에서 시작한다. 레포 루트에서 시작한 Codex는 `backend/.agents/skills/`의 스킬을 찾지 못한다.
- `backend/` 작업은 이 문서를 따른다. `~/AGENTS.md` 등 상위 지침과 충돌하면 이 문서가 우선한다.
- `backend/` 밖의 파일은 수정하지 않는다. 예외는 앱↔서버 API 명세인 루트 `docs/api/`뿐이다. 그 밖이 필요하면 수정하지 말고 사용자에게 알린다.
- 기획 판단이 필요하면 PM vault의 PRD를 먼저 읽는다:
  `/Users/stecdev/Desktop/obsidian-workspace/personal_dev/wiki/projects/ride-mate/prd.md`

## 작업 전에 읽을 문서

| 작업 | 문서 |
|---|---|
| 폴더 구조·계층 방향·Program.cs 순서·DI 등록 | `docs/architecture/overview.md` |
| Controller | `docs/architecture/controller.md` |
| Service | `docs/architecture/service.md` |
| Repository·쿼리(EF Core / Dapper) | `docs/architecture/repository.md` |
| DTO | `docs/architecture/dto.md` |
| 테이블·스키마·DbContext·로컬 DB | `docs/architecture/database.md` |
| 라우트·응답 code·커서 페이징·API 명세 | `docs/api.md` |
| 설정·비밀값·로그·입력 처리·인증 | `docs/security.md` |
| 네이밍·코드 스타일 | `docs/coding-style.md` |

## 스킬

| 스킬 | 쓰는 때 |
|---|---|
| `start-task` | 작업을 시작할 때 — 카드 확인·생성, wip 전환, 브랜치 생성 |
| `add-domain` | 도메인이나 API를 추가할 때 |
| `db-schema-change` | 테이블을 만들거나 바꿀 때 |
| `update-api-spec` | 라우트·DTO를 바꾼 뒤 `docs/api/openapi.json`을 갱신할 때 |

- 스킬 정본은 `.agents/skills/<이름>/SKILL.md`다. `.claude/skills/<이름>`은 정본을 가리키는 심볼릭 링크이므로 스킬은 `.agents/skills/`에서만 고친다.
- 코드 템플릿은 스킬 폴더의 `templates/`에만 둔다. 문서에는 규칙과 템플릿 경로만 쓴다. 레이어 규칙의 정본은 `docs/`다.
- 스킬을 추가하면 `backend/`에서 같은 이름으로 링크를 만든다: `ln -s ../../.agents/skills/<이름> .claude/skills/<이름>`

## 핵심 규칙

- 솔루션은 `RideMateService/RideMateService.sln`, 앱 프로젝트는 `RideMateService/RideMateService/`다.
- 의존 방향은 Controller → Service → Repository 하나뿐이다. 역참조·레이어 건너뛰기를 하지 않는다.
- Service는 항상 `ResponseModel<T>`를 반환한다. 예외를 던지지 않고 `code = 500`으로 바꾼다(요청 취소만 그대로 올려보낸다).
- Repository는 DTO를 받지 않는다. Dapper 조회 결과를 응답 DTO로 바로 매핑하는 것만 허용한다.
- GET API의 조회에는 EF Core를 쓰지 않고 Dapper로 SQL을 직접 쓴다(쿼리 튜닝과 속도). EF Core는 등록·수정·삭제에만 쓴다.
- 스키마는 DB-first다. EF Migration을 쓰지 않고, 스캐폴드 산출물(`Commons/DBModels`, `RideMateContext.cs`)을 직접 고치지 않는다.
- 비밀값을 코드나 `appsettings.json`에 쓰지 않는다.

## 빌드·실행·검증

명령은 `backend/RideMateService/`에서 실행한다.

- 빌드: `dotnet build RideMateService.sln -warnaserror`
- 실행: `dotnet run --project RideMateService --launch-profile http` → `http://localhost:5167/swagger`
- 작업을 끝내기 전에 빌드가 경고 0으로 통과해야 한다.
- 엔드포인트·DTO를 바꿨으면 서버를 띄워 Swagger에서 그 API를 호출해 응답을 확인하고, `update-api-spec` 스킬로 명세를 같은 PR에 넣는다.
- 띄운 서버는 PID로 종료한다(`lsof -ti :5167` → `kill <PID>`). `pkill`처럼 이름으로 종료하지 않는다.
- 테스트 프로젝트는 두지 않는다(STSP 표준).

## 작업 흐름

- `main`에 직접 커밋하지 않는다. 작업마다 `main`에서 브랜치를 만들고 PR로 머지한다. 시작은 `start-task` 스킬로 한다.
- 기능(feat)·버그(fix) 작업은 PM vault 작업 카드 단위로 한다. 카드 위치: `/Users/stecdev/Desktop/obsidian-workspace/personal_dev/wiki/projects/ride-mate/tasks/`
  - 카드가 없으면 먼저 만든다. 빈 번호를 `ls`로 확인한다. 쓰인 번호를 재사용하지 않는다.
  - 브랜치 이름에 카드 ID를 넣는다: `feat/T01-<설명>`, `fix/T02-<설명>`.
  - 착수할 때 `wip`, 머지한 뒤 `done`으로 바꾼다.
- 설정·문서 같은 잡일은 카드 없이 `chore/<설명>`·`docs/<설명>` 브랜치로 한다.

```
VAULT=/Users/stecdev/Desktop/obsidian-workspace/personal_dev
bash "$VAULT/.claude/scripts/task.sh" new  T0x "<제목>" --project ride-mate --section code
bash "$VAULT/.claude/scripts/task.sh" wip  T0x --project ride-mate --section code
bash "$VAULT/.claude/scripts/task.sh" done T0x --project ride-mate
```

- 커밋 메시지는 `<type>: <한국어 요약>` 형식이다. type은 feat·fix·refactor·docs·chore 중 하나다.
- pre-commit 훅을 우회하지 않는다(`--no-verify` 금지).

## 커밋 훅

- 클론한 뒤 레포 루트에서 한 번 설치한다: `ln -s ../../backend/githooks/pre-commit .git/hooks/pre-commit`
- `git config core.hooksPath`는 바꾸지 않는다.
