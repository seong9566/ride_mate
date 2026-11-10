# Database

DB는 MySQL, EF Core 공급자는 `MySql.EntityFrameworkCore`, 스키마는 DB-first다. 스키마를 바꾸는 절차 전체는 `db-schema-change` 스킬을 따른다.

## 테이블 규칙

- 테이블·컬럼 이름은 대문자 스네이크로 쓴다: `PICKUP_INFO`, `PICKUP_ID`. 소문자를 섞지 않는다.
- PK는 `{도메인}_ID`로 짓는다. 불리언 컬럼은 `_YN` 접미에 `TINYINT(1)`을 쓴다.
- 모든 업무 테이블에 감사 컬럼 `CREATE_DT`·`CREATE_USER`·`UPDATE_DT`·`UPDATE_USER`·`DELETE_YN`을 둔다.
- 물리 삭제하지 않는다. `DELETE_YN = TRUE`로 바꾸고 `UPDATE_DT`·`UPDATE_USER`도 갱신한다.
- 모든 조회에 `DELETE_YN = FALSE` 조건을 넣는다. EF도 `.Where(m => m.DeleteYn == false)`를 붙인다.
  - 예외는 `PICKUP_PLAN`·`PICKUP_SESSION`·`PICKUP_EVENT` 세 테이블이다. 여기서 `DELETE_YN`은 "끝나서 익명화됨"을 뜻하므로 조건을 붙이지 않고 계획의 `STATUS`로 가린다. [ADR-0008]
- 어떤 테이블과 컬럼을 두는지는 `schema.md`에 있다.

## DB-first

- 스키마는 DB에서 먼저 만든다. EF Migration을 쓰지 않는다. [ADR-0002]
- DDL은 `backend/sql/{yyyyMMdd}_{설명}.sql`로 남긴다. 새 테이블은 `.agents/skills/db-schema-change/templates/table.sql`에서 시작한다. 스키마 변경 이력은 이 폴더에만 남긴다.
- `Commons/DBModels/*.cs`와 `Commons/Repository/RideMateContext.cs`는 직접 고치지 않는다. 재스캐폴드하면 덮어쓰인다.
- 손으로 추가하는 코드(쿼리 헬퍼·확장)는 `Commons/Repository/RideMateContext.Extensions.cs`에 partial로 쓴다.
- 재스캐폴드 전 백업 파일(`.bak`)을 만들지 않는다. 변화는 `git diff`로 확인한다.
- `dotnet-ef`는 EF Core와 같은 10.x 버전을 `RideMateService/`의 로컬 도구 매니페스트로 쓴다.
- 스캐폴드는 `POLYGON`·`LINESTRING` 컬럼을 건너뛴다. 공급자가 이 타입을 지원하지 않는다. 엔티티에 없는 공간 컬럼은 Dapper로만 읽고 쓴다. `GEOMETRY` 컬럼은 엔티티에 있지만 점만 담는 타입으로 잡히므로 마찬가지로 Dapper로 쓴다. 공간 컬럼이 있는 `AREA`·`LOOP_ROUTE`·`RESTRICTED_ZONE`은 등록·수정·삭제 표시를 모두 Dapper로 한다. [ADR-0011]
- 생성 컬럼 `ACTIVE_FLAG`는 `RideMateContext.Extensions.cs`에서 저장 대상에서 뺀다. 이름이 `ActiveFlag`인 속성에 모두 적용되므로 테이블을 추가해도 고칠 필요가 없다. [ADR-0010]
- 로컬 MySQL은 Docker로 띄운다. compose 파일은 `backend/docker-compose.yml`(템플릿 `.agents/skills/db-schema-change/templates/docker-compose.yml`), 비밀번호는 git에서 제외된 `backend/.env`에 둔다.

## DbContext 등록

Dapper가 DbContext와 같은 커넥션을 쓰도록 `IDbConnection`을 DbContext에서 꺼내 등록한다. 템플릿: `.agents/skills/db-schema-change/templates/DbContextRegistration.cs`.

- 연결 문자열이 비어 있으면 기동할 때 `InvalidOperationException`으로 실패시킨다(`../security.md`).
