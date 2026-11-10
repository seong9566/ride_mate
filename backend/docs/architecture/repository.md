# Repository 레이어

위치: `Repository/v1/{도메인}/`. 한 클래스를 partial 파일 3개로 나눠 EF 코드와 SQL 문자열이 한 파일에 섞이지 않게 한다.

| 파일 | 내용 |
|---|---|
| `I{도메인}Repository.cs` | EF 메서드 → `// ====== Dapper` 주석 → Dapper 메서드 순 |
| `{도메인}Repository.cs` | partial, 생성자·필드만 |
| `{도메인}Repository.EF.cs` | partial, EF Core 구현 |
| `{도메인}Repository.SQL.cs` | partial, Dapper 구현 |

## EF Core와 Dapper 분담

| 작업 | 도구 | 파일 |
|---|---|---|
| GET API의 조회 전부(목록·상세·검색·집계·JOIN) | Dapper | `.SQL.cs` |
| 등록·수정·삭제 | EF Core | `.EF.cs` |
| 수정·삭제할 대상 엔티티 불러오기(PK 기준) | EF Core | `.EF.cs` |
| 행을 잠그는 조회(`SELECT … FOR UPDATE`) | Dapper | `.SQL.cs` |
| 트랜잭션 | EF Core (`BeginTransactionAsync`) | Service에서 시작한다(`service.md`) |

- GET API의 조회에는 ORM(EF Core)을 쓰지 않는다. Dapper로 SQL을 직접 쓴다. 쿼리를 튜닝할 수 있고 더 빠르기 때문이다. 단건 상세 조회도 Dapper로 쓴다. [ADR-0003]
- EF Core는 쓰기(등록·수정·삭제)에만 쓴다. 수정·삭제할 대상 엔티티를 PK로 불러오는 것은 쓰기의 일부이므로 EF Core로 한다.
- 한 메서드 안에서 둘을 섞지 않는다.
- 행을 잠그는 조회는 EF Core LINQ로 쓸 수 없어 Dapper로 쓴다. 트랜잭션의 첫 잠금 조회는 첫 문장으로 부르고, 이어지는 쓰기는 EF Core로 한다. 잠그는 순서는 `schema.md`를 따른다. 픽업 지점 배정과 세션 상태 바꾸기가 여기에 해당한다. [ADR-0009]

## 템플릿

| 파일 | 템플릿 |
|---|---|
| `I{도메인}Repository.cs` | `.agents/skills/add-domain/templates/IRepository.cs` |
| `{도메인}Repository.cs` | `.agents/skills/add-domain/templates/Repository.cs` |
| `{도메인}Repository.EF.cs` | `.agents/skills/add-domain/templates/Repository.EF.cs` (등록) |
| `{도메인}Repository.SQL.cs` | `.agents/skills/add-domain/templates/Repository.SQL.cs` (커서 페이징 목록 조회, 규칙은 `../api.md`) |

새 Repository와 메서드는 이 템플릿의 구조를 그대로 쓴다.

## 규칙

- Repository는 엔티티만 받고 돌려준다. DTO를 받거나 돌려주지 않는다. 예외: Dapper 조회는 응답 DTO로 바로 매핑해도 된다.
- 예외는 로그를 남긴 뒤 `throw;`로 다시 던진다. `false`·`null`로 삼키지 않는다. 요청이 취소된 경우에는 로그 없이 그대로 올려보낸다(템플릿 catch의 `when` 조건).
- Dapper 값은 반드시 `DynamicParameters`로 넘긴다. 문자열 보간은 절(clause) 조립에만 쓴다.
- `LIKE` 검색어는 `\`·`%`·`_`를 이스케이프하고 `ESCAPE '\\'`를 붙인다.
- 모든 메서드는 마지막 파라미터로 `CancellationToken cancellationToken = default`를 받아 EF Core와 Dapper 호출에 넘긴다.
- SELECT 컬럼 별칭은 DTO 프로퍼티 이름(camelCase)에 맞춘다: `SELECT t.PICKUP_ID AS pickupSeq`.
- 모든 조회에 `DELETE_YN = FALSE` 조건을 넣는다. 예외는 `database.md`에 있다.

## csproj 등록

3분할 파일이 IDE에서 하나로 접히도록 도메인을 추가할 때마다 `RideMateService.csproj`에 `DependentUpon` 항목 2개를 넣는다. 템플릿: `.agents/skills/add-domain/templates/DependentUpon.csproj.xml`.
