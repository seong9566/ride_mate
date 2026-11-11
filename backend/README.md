# RideMate 백엔드

ASP.NET Core Web API(.NET 10) 서버입니다. DB는 MySQL 8.4를 쓰고, 스키마는 DB-first로 관리합니다.

- 코드 구조와 작업 규칙: [`AGENTS.md`](AGENTS.md), [`docs/`](docs/)
- 테이블 설계: [`docs/architecture/schema.md`](docs/architecture/schema.md)
- 앱↔서버 API 명세: 루트 [`docs/api/`](../docs/api/)(`openapi.json`), API 문서 정본은 Notion "라이드메이트 API 문서"(https://app.notion.com/p/415aa638eaf880808d84c4ffba30bb51)

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
├── docker-compose.yml             # 로컬 MySQL · 배포용 API(deploy 프로필)
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

`main`·`backend`·`backend-dev` 브랜치에서 `backend/` 변경을 커밋하지 못하게 막고, 커밋할 때마다 경고를 오류로 보고 빌드합니다.

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

### 5. 로컬 설정

`RideMateService/RideMateService/appsettings.Development.json`을 만듭니다. git에서 제외되는 파일입니다.

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=127.0.0.1;Port=3306;Database=ridemate;User=ridemate;Password=<.env의 MYSQL_PASSWORD>;"
  },
  "Jwt": {
    "Issuer": "ridemate-local",
    "Audience": "ridemate-car",
    "SigningKey": "<base64 32바이트>"
  },
  "Crypto": {
    "PhoneEncKey": "<base64 32바이트>",
    "HashKey": "<base64 32바이트>"
  }
}
```

`.env`에 `MYSQL_PORT`를 적었으면 `Port=3306`을 그 값으로 바꿉니다.

세 키는 각각 다른 값으로 만듭니다. 키 하나를 만드는 명령은 다음과 같고, 세 번 실행해 각 자리에 넣습니다. 출력된 값은 파일에만 두고 커밋하지 않습니다.

```bash
openssl rand -base64 32
```

연결 문자열과 `Jwt`·`Crypto`의 다섯 값 중 하나라도 비어 있으면 서버가 기동할 때 실패합니다.

## 빌드·실행

`backend/RideMateService/`에서 실행합니다.

```bash
dotnet build RideMateService.sln -warnaserror              # 경고 0이어야 합니다
dotnet run --project RideMateService --launch-profile http # http://localhost:5167/swagger
```

5167 포트가 이미 쓰이고 있으면 `dotnet run --project RideMateService --urls http://localhost:<빈 포트>`로 띄웁니다.

## 배포 (맥 + Docker + Tailscale Funnel)

API 서버를 컨테이너로 띄우고 Tailscale Funnel로 공개 HTTPS 주소를 붙입니다. 차량 앱과 아이 휴대폰은 tailnet에 들어올 수 없으므로 Funnel(공개)을 씁니다. 명령은 `backend/`에서 실행합니다.

1. `.env`에 `.env.example`의 배포 항목을 채웁니다. `PUBLIC_BASE_URL`은 4단계에서 주소를 확인한 뒤 채웁니다.
2. 서버를 띄웁니다. 컨테이너 시간대는 KST(`TZ=Asia/Seoul`)입니다.
   ```bash
   docker compose --profile deploy up -d --build
   curl -s --retry 15 --retry-all-errors --retry-delay 1 -o /dev/null -w '%{http_code}\n' http://127.0.0.1:8080/swagger/index.html   # 200
   docker logs ridemate-api --tail 50
   ```
3. Tailscale 관리 콘솔에서 MagicDNS와 HTTPS Certificates를 켭니다.
4. Funnel을 켭니다. 처음 실행하면 정책 파일에 `funnel` 속성을 추가하라는 링크가 나옵니다.
   ```bash
   tailscale funnel --bg 8080
   tailscale funnel status    # https://<기기>.<tailnet>.ts.net
   ```
5. `.env`의 `PUBLIC_BASE_URL`에 4단계 주소를 넣고 `docker compose --profile deploy up -d`로 다시 띄웁니다. `docker exec ridemate-api printenv QrLogin__VerificationUri`가 localhost가 아니라 공개 주소인지 확인합니다.
6. 맥이 잠들지 않게 하고, Docker Desktop과 Tailscale이 로그인할 때 자동으로 실행되게 합니다. 컨테이너는 `restart: unless-stopped`라 Docker가 뜨면 같이 뜹니다. FileVault가 켜져 있으면 재부팅 뒤 직접 로그인해야 Docker가 뜹니다.
7. 휴대폰 LTE(와이파이 끔)로 `https://<기기>.<tailnet>.ts.net/swagger`가 열리는지 확인합니다.

- 위 명령의 8080은 `API_PORT`를 비워 둔 경우입니다. 바꿨으면 그 값을 씁니다.
- 코드를 바꾼 뒤에는 2단계 명령(`--build`)으로 다시 띄웁니다. 개발용 `docker compose up -d`는 `deploy` 프로필이 없어 API 컨테이너를 띄우지 않습니다.
- 배포 서버는 개발과 같은 MySQL 컨테이너를 씁니다. 배포 중에는 `docker compose down -v`나 위 "4. 스키마 적용"의 DB 초기화(DROP)를 하지 않습니다. 서버만 내릴 때는 `docker compose --profile deploy stop api`를 씁니다.

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

- 통합 브랜치는 `backend-dev`입니다. 작업 브랜치는 `backend-dev`에서 만들고 PR도 `backend-dev`로 엽니다.
- `backend`는 배포 브랜치입니다. 지시가 있을 때 `backend-dev` → `backend` PR(머지 커밋)로 반영하면 자동 배포됩니다.
- `main` 반영은 따로 지시가 있을 때 `backend` → `main` PR로 합니다.
- 자세한 규칙은 [`AGENTS.md` §작업 흐름](AGENTS.md#작업-흐름)에 있습니다.
