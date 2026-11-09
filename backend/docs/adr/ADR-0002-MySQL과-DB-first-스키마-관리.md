# ADR-0002 — MySQL과 DB-first 스키마 관리

- 상태: **채택** (2026-11-09)

## 결정

DB는 MySQL이다. 스키마는 DB에서 먼저 만들고 EF Core로 스캐폴드한다. EF Migration은 쓰지 않는다. DDL은 `backend/sql/`에 날짜가 붙은 파일로 쌓는다. EF Core 공급자는 Oracle의 `MySql.EntityFrameworkCore`를 쓴다.

## 근거

- DB-first와 수동 DDL은 STSP 표준이다([ADR-0001]).
- Pomelo 공급자는 EF Core 9까지만 지원해 .NET 10에서 쓸 수 없었다.
- Supabase는 검토한 뒤 제외했다. 배포처는 아직 정하지 않았고, 무료 플랜이어야 한다.

## 결과

- 스캐폴드 산출물(`Commons/DBModels`, `RideMateContext.cs`)은 손으로 고치지 않는다.
- 어느 환경에 어떤 DDL이 적용됐는지는 기록되지 않는다. 환경이 둘 이상이 되면 적용 이력 관리를 다시 정한다.
- 로컬 DB는 Docker로 띄운다(`db-schema-change` 스킬).
