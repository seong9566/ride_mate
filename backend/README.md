# RideMate 백엔드

ASP.NET Core Web API(.NET 10) 서버입니다. DB는 MySQL 8.4를 쓰고, 스키마는 DB-first로 관리합니다.

- 코드 구조와 작업 규칙: [`AGENTS.md`](AGENTS.md), [`docs/`](docs/)
- 테이블 설계: [`docs/architecture/schema.md`](docs/architecture/schema.md)
- 앱↔서버 API 명세: 루트 [`docs/api/`](../docs/api/)

## 개발 환경

| 도구 | 버전 | 용도 |
|---|---|---|
| .NET SDK | 10.x | 빌드·실행 |
| Docker Desktop | 최신 | 로컬 MySQL 8.4 컨테이너 |
| `dotnet-ef` | 10.0.14 | DB-first 스캐폴드. `RideMateService/dotnet-tools.json`의 로컬 도구라 따로 설치하지 않습니다 |

```
backend/
├── RideMateService/
│   ├── RideMateService.sln
│   ├── dotnet-tools.json          # dotnet-ef 로컬 도구
│   └── RideMateService/           # 앱 프로젝트
├── sql/                           # DDL. 파일 이름 순서대로 적용
├── docker-compose.yml             # 로컬 MySQL
├── .env.example                   # .env 템플릿
├── githooks/ · scripts/           # pre-commit 훅과 설치 스크립트
└── docs/                          # 아키텍처·규칙·ADR
```

## 처음 설정

명령은 따로 적지 않으면 `backend/`에서 실행합니다.

### 1. pre-commit 훅 설치 (클론한 뒤 한 번)

```bash
sh scripts/install-hooks.sh
```

`main`·`backend` 브랜치에서 `backend/` 변경을 커밋하지 못하게 막고, 커밋할 때마다 경고를 오류로 보고 빌드합니다.

### 2. `.env` 만들기

```bash
cp .env.example .env
```

`MYSQL_ROOT_PASSWORD`·`MYSQL_PASSWORD`에 임의의 값을 넣습니다. 3306 포트를 다른 MySQL이 쓰고 있으면 `MYSQL_PORT`에 빈 포트(예: `3308`)를 적습니다. 비워 두면 3306입니다.

- `.env`는 git에서 제외됩니다. 커밋하지 않습니다.
- DB 볼륨은 처음 띄울 때의 비밀번호로 만들어집니다. 그 뒤에 `.env`의 비밀번호를 바꾸면 접속이 안 되므로 바꾸지 않습니다.

### 3. 로컬 MySQL 띄우기

```bash
lsof -ti :<MYSQL_PORT>          # 출력이 없어야 합니다(포트가 비어 있음)
docker compose up -d --wait     # healthcheck가 통과할 때까지 기다립니다
```

### 4. 스키마 적용

새로 띄운 빈 DB에 `sql/`의 파일을 **이름 순서대로** 모두 적용합니다. 한 파일이라도 실패하면 그 자리에서 멈춥니다.

```bash
for f in sql/*.sql; do
  echo "== $f"
  docker compose exec -T mysql sh -c 'mysql --default-character-set=utf8mb4 -uridemate -p"$MYSQL_PASSWORD" ridemate' < "$f" || { echo "실패: $f"; break; }
done
```

- `--default-character-set=utf8mb4`를 빼면 한국어 COMMENT가 깨진 채 저장됩니다.
- 이 루프는 빈 DB 전용입니다. 이미 적용한 DB에서 다시 돌리면 `CREATE TABLE`이 중복 오류를 냅니다.
- 중간에 실패했으면 이어서 돌리지 말고 DB를 비운 뒤 처음부터 다시 적용합니다. DDL은 문장마다 바로 반영돼 되돌려지지 않습니다.

```bash
docker compose exec -T mysql sh -c 'mysql -uroot -p"$MYSQL_ROOT_PASSWORD" -e "DROP DATABASE ridemate; CREATE DATABASE ridemate CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;"'
```

### 5. 연결 문자열 설정

`RideMateService/RideMateService/appsettings.Development.json`을 만듭니다. git에서 제외되는 파일입니다.

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=127.0.0.1;Port=3306;Database=ridemate;User=ridemate;Password=<.env의 MYSQL_PASSWORD>;"
  }
}
```

`.env`에 `MYSQL_PORT`를 적었으면 `Port=3306`을 그 값으로 바꿉니다.

연결 문자열이 없으면 서버가 기동할 때 실패합니다.

## 빌드·실행

`backend/RideMateService/`에서 실행합니다.

```bash
dotnet build RideMateService.sln -warnaserror              # 경고 0이어야 합니다
dotnet run --project RideMateService --launch-profile http # http://localhost:5167/swagger
```

5167 포트가 이미 쓰이고 있으면 `dotnet run --project RideMateService --urls http://localhost:<빈 포트>`로 띄웁니다.

## 스키마를 바꿀 때

1. `sql/{yyyyMMdd}_{설명}.sql`로 새 DDL 파일을 만듭니다. 이미 적용한 파일은 고치지 않습니다.
2. **새로 만든 파일 하나만** 로컬 DB에 적용합니다. 4단계의 루프를 다시 돌리지 않습니다.

```bash
docker compose exec -T mysql sh -c 'mysql --default-character-set=utf8mb4 -uridemate -p"$MYSQL_PASSWORD" ridemate' < sql/{새 파일}.sql
```


3. 엔티티를 다시 스캐폴드합니다(`RideMateService/`에서 `dotnet tool restore` 후, `RideMateService/RideMateService/`에서 실행).

```bash
dotnet ef dbcontext scaffold Name=ConnectionStrings:DefaultConnection MySql.EntityFrameworkCore \
  --output-dir Commons/DBModels \
  --context-dir Commons/Repository \
  --context RideMateContext \
  --no-onconfiguring \
  --force
```

- `Commons/DBModels`와 `Commons/Repository/RideMateContext.cs`는 스캐폴드 산출물이라 손으로 고치지 않습니다.
- `--force`는 없어진 테이블의 엔티티 파일을 지우지 않습니다. 테이블을 지우거나 이름을 바꿨으면 옛 파일을 직접 지웁니다.
- 자세한 절차와 확인 항목은 [`db-schema-change` 스킬](.agents/skills/db-schema-change/SKILL.md)을 따릅니다.

## 작업 흐름

- 통합 브랜치는 `backend`입니다. 작업 브랜치는 `backend`에서 만들고 PR도 `backend`로 엽니다.
- `main` 반영은 따로 지시가 있을 때 `backend` → `main` PR로 합니다.
- 자세한 규칙은 [`AGENTS.md` §작업 흐름](AGENTS.md#작업-흐름)에 있습니다.
