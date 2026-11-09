---
name: db-schema-change
description: RideMate 백엔드의 MySQL 테이블을 만들거나 바꿀 때 쓴다. DDL 파일 작성, 로컬 MySQL(Docker) 적용, EF Core DB-first 재스캐폴드, 변경 확인까지 한다. "테이블 추가", "컬럼 추가", "스키마 변경", "스캐폴드"에 쓴다.
---

# db-schema-change

테이블 규칙의 정본은 `backend/docs/architecture/database.md`다. 먼저 읽는다.

## 하지 말 것

- `main` 브랜치에서 시작하지 않는다. `main`이면 멈추고 `start-task` 스킬로 브랜치부터 만든다.
- 이미 적용된 DDL 파일을 고치지 않는다. 변경분은 새 파일로 쓴다.
- EF Migration을 쓰지 않는다. 스캐폴드 산출물(`Commons/DBModels/*.cs`, `Commons/Repository/RideMateContext.cs`)을 손으로 고치지 않는다.
- 비밀번호를 명령줄, 채팅, 로그에 쓰지 않는다. 스캐폴드가 설정을 못 읽어 실패해도 명령줄에 비밀번호를 넣어 우회하지 않고, 멈추고 사용자에게 알린다.
- 3306 포트를 다른 MySQL이 쓰고 있으면 그 프로세스를 종료하지 않는다. 멈추고 사용자에게 알린다.
- `.bak` 백업 파일을 만들지 않는다. 변화는 `git diff`로 본다.

## 만들 파일

템플릿은 이 스킬 폴더의 `templates/`에 있다. 경로는 `backend/` 기준이다.

| 템플릿 | 만들 파일 | 만드는 때 |
|---|---|---|
| `templates/table.sql` | `sql/{yyyyMMdd}_{설명}.sql` | 매번. 새 테이블은 템플릿의 감사 컬럼을 그대로 둔다 |
| `templates/docker-compose.yml` | `docker-compose.yml` | 없을 때 한 번. 내용을 그대로 복사한다 |
| `templates/env.example` | `.env.example`, `.env` | 없을 때 한 번 |
| `templates/DbContextRegistration.cs` | `RideMateService/RideMateService/Program.cs`에 추가 | 처음 스캐폴드했을 때 한 번 |

`table.sql`의 자리표시자는 `{테이블}`, `{PK}`(`{도메인}_ID`), `{한글 설명}`이다. 이름은 대문자 스네이크로 쓴다.

## 절차

명령은 따로 적지 않으면 `backend/`에서 실행한다.

1. DDL 파일을 만든다. 자리표시자가 남지 않았는지 본다. 아래 명령의 출력이 없어야 한다.
   `grep -nE '\{(테이블|PK|한글)' sql/{파일}.sql`
2. 로컬 MySQL을 준비한다.
   - `docker-compose.yml`이 없으면 템플릿을 복사한다.
   - `.env`가 없으면 `.env.example`을 템플릿으로 만들고, `.env`에는 `MYSQL_ROOT_PASSWORD`와 `MYSQL_PASSWORD`를 무작위 값으로 채운다. `.env`는 git에서 제외되어 있고 `.env.example`은 커밋한다.
   - `.env`를 새로 만들 때는 같은 셸 호출에서 같은 변수로 아래 `appsettings.Development.json`도 쓴다. 만든 뒤 `.env`를 다시 읽어 값을 옮기지 않는다.
   - `RideMateService/RideMateService/appsettings.Development.json`의 `ConnectionStrings:DefaultConnection`을 `Server=127.0.0.1;Port=3306;Database=ridemate;User=ridemate;Password=<.env의 MYSQL_PASSWORD>;`로 맞춘다. 이 파일도 git에서 제외되어 있다.
   - `lsof -ti :3306`으로 포트를 확인한 뒤 `docker compose up -d --wait`로 띄운다. `--wait`는 compose의 healthcheck가 통과할 때까지, 곧 MySQL이 접속을 받을 때까지 기다린다.
3. DDL을 적용한다.

```bash
docker compose exec -T mysql sh -c 'mysql -uridemate -p"$MYSQL_PASSWORD" ridemate' < sql/{파일}.sql
```

4. `RideMateService/`에서 `dotnet-ef`를 준비한다. `.config/dotnet-tools.json`이 없으면 `dotnet new tool-manifest` 후 `dotnet tool install dotnet-ef --version 10.0.14`, 있으면 `dotnet tool restore`.
5. `RideMateService/RideMateService/`에서 스캐폴드한다. 명령을 바꾸지 않는다.

```bash
dotnet ef dbcontext scaffold Name=ConnectionStrings:DefaultConnection MySql.EntityFrameworkCore \
  --output-dir Commons/DBModels \
  --context-dir Commons/Repository \
  --context RideMateContext \
  --no-onconfiguring \
  --force
```

6. 처음 스캐폴드했다면 `templates/DbContextRegistration.cs`의 내용을 `Program.cs`에 넣고, `Commons/DBModels/`와 `Commons/Repository/`의 `.gitkeep`을 지운다.
7. `git diff`로 산출물을 본다. 이번 DDL과 관계없는 엔티티가 바뀌었으면 멈추고 사용자에게 알린다.
8. `RideMateService/`에서 `dotnet build RideMateService.sln -warnaserror`를 실행한다.
9. DDL 파일과 스캐폴드 산출물을 스테이징하고, 커밋하기 전에 아래 둘을 확인한다.
   - `git diff --cached --name-only`에 `.env`와 `appsettings.Development.json`이 없다.
   - 스테이징된 내용에 값이 채워진 비밀번호가 없다. 아래 명령이 `0`을 출력해야 한다(줄 수만 세고 내용은 출력하지 않는다).
     `git diff --cached | grep -ciE '(password|pwd)=[^;"<]'`
10. DDL 파일과 스캐폴드 산출물을 같은 커밋에 넣는다.

## 완료 조건

아래를 모두 확인한 뒤에 끝났다고 보고한다.

- [ ] DDL 파일이 `sql/{yyyyMMdd}_{설명}.sql`로 있고, 새 테이블에 감사 컬럼 5개가 있고, 자리표시자가 남아 있지 않다.
- [ ] DDL이 로컬 DB에 오류 없이 적용됐다.
- [ ] `git diff`에 이번 DDL에 해당하는 엔티티 변화만 있다.
- [ ] `dotnet build RideMateService.sln -warnaserror`가 경고 0, 오류 0이다.
- [ ] DDL 파일과 스캐폴드 산출물이 한 커밋에 들어 있고, 그 커밋에 `.env`와 `appsettings.Development.json`이 없다(`git show --stat --format= HEAD`).
