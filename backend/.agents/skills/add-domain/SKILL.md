---
name: add-domain
description: RideMate 백엔드에 새 도메인(DTO·Repository·Service·Controller 한 벌)을 추가하거나 기존 도메인에 API를 추가할 때 쓴다. "API 추가", "도메인 추가", "엔드포인트 만들어"에 쓴다.
---

# add-domain

명령은 `backend/RideMateService/`에서 실행한다. 파일 경로는 앱 프로젝트 `RideMateService/RideMateService/` 기준이다.

## 하지 말 것

- `main`이나 `backend` 브랜치에서 시작하지 않는다. 그 브랜치면 멈추고 `start-task` 스킬로 카드와 브랜치부터 만든다.
- 엔티티를 손으로 만들지 않는다. 필요한 엔티티가 `Commons/DBModels/`에 없으면 멈추고 `db-schema-change` 스킬로 테이블부터 만든다.
- 엔티티 클래스 이름과 `{도메인}`이 같으면 진행하지 않는다(예: 둘 다 `Pickup`). 도메인 네임스페이스가 엔티티 이름을 가려 빌드가 실패한다(CS0118). 멈추고 사용자에게 알린다.
- 템플릿의 구조(try/catch, 반환 타입, 응답 문구, `[Produces]`·`[SwaggerOperation]`·`[ProducesResponseType]`)를 바꾸지 않는다. 바꾸는 것은 자리표시자, 업무 필드, 쿼리, 메서드 목록, 동작에 따른 HTTP 메서드 어트리뷰트와 라우트(`api.md`의 표), 그리고 `[ProducesResponseType]`의 응답 타입뿐이다.
- 아래 표와 다른 위치나 이름으로 파일을 만들지 않는다.

## 만들 파일

템플릿은 이 스킬 폴더의 `templates/`에 있다. 파일을 만들기 전에 해당 템플릿을 읽고 그 구조를 그대로 쓴다. 레이어 규칙은 표의 규칙 문서(`backend/docs/` 기준)가 정본이므로 함께 읽는다.

| # | 템플릿 | 만들 파일 | 규칙 문서 |
|---|---|---|---|
| 1 | `templates/RequestDto.cs` | `Comm/Dto/v1/{도메인}/Request/{동작}{도메인}Dto.cs` | `architecture/dto.md` |
| 2 | `templates/ListResponseDto.cs` | `Comm/Dto/v1/{도메인}/Response/Get{도메인}List.cs` (상세는 `Detail{도메인}Dto.cs`) | `architecture/dto.md` |
| 3 | `templates/IRepository.cs` | `Repository/v1/{도메인}/I{도메인}Repository.cs` | `architecture/repository.md` |
| 4 | `templates/Repository.cs` | `Repository/v1/{도메인}/{도메인}Repository.cs` | `architecture/repository.md` |
| 5 | `templates/Repository.EF.cs` | `Repository/v1/{도메인}/{도메인}Repository.EF.cs` | `architecture/repository.md` |
| 6 | `templates/Repository.SQL.cs` | `Repository/v1/{도메인}/{도메인}Repository.SQL.cs` | `architecture/repository.md` |
| 7 | `templates/IService.cs` | `Service/v1/{도메인}/I{도메인}Service.cs` | `architecture/service.md` |
| 8 | `templates/Service.cs` | `Service/v1/{도메인}/{도메인}Service.cs` | `architecture/service.md` |
| 9 | `templates/Controller.cs` | `Controller/v1/{도메인}/{도메인}Controller.cs` | `architecture/controller.md`, `api.md` |
| 10 | `templates/ServiceCollectionExtensions.cs` | `Extensions/ServiceCollectionExtensions.cs` | `architecture/overview.md` |
| 11 | `templates/DependentUpon.csproj.xml` | `RideMateService.csproj`에 추가 | `architecture/repository.md` |

템플릿은 등록(`Add`, EF Core)과 목록 조회(`Get…List`, Dapper 커서 페이징) 두 가지를 담고 있다. 필요 없는 쪽은 지우고, 다른 동작은 같은 형태로 더한다. 메서드·액션 이름은 `api.md`와 `coding-style.md`를 따른다.

| 자리표시자 | 뜻 | 예 |
|---|---|---|
| `{도메인}` | 도메인 이름, PascalCase 단수 | `Pickup` |
| `{도메인소문자}` | 도메인 이름, camelCase | `pickup` |
| `{엔티티}` | 스캐폴드된 엔티티 클래스 | `PickupInfo` |
| `{엔티티복수}` | `RideMateContext`의 DbSet 이름 | `PickupInfos` |
| `{테이블}`, `{PK}` | 테이블 이름, PK 컬럼 이름 | `PICKUP_INFO`, `PICKUP_ID` |
| `{한글 설명}` | 요약 주석과 Swagger Summary | `픽업 등록` |

위 자리표시자만 바꾼다. `{cursorClause}`처럼 영문으로 된 중괄호는 C# 문자열 보간이므로 그대로 둔다.

## 절차

1. 표의 규칙 문서와 템플릿을 읽는다.
2. 1~9번 파일을 순서대로 만든다. 기존 도메인에 API만 추가하면 해당 파일에 메서드를 더한다.
3. 새 도메인이면 DI를 등록한다(10번). 기존 도메인에 API만 추가하면 이 단계와 4번을 건너뛴다.
   - `Extensions/ServiceCollectionExtensions.cs`가 없으면 템플릿으로 만든다. `Program.cs`에 `using RideMateService.Extensions;`를 더하고, `ILogService` 등록을 지운 자리에서 `builder.Services.AddDomainModules();`와 `builder.Services.AddCommonInfrastructure();`를 호출한다.
   - 이미 있으면 `Add{도메인}Module()`을 더하고 `AddDomainModules()`에 한 줄 추가한다.
4. `RideMateService.csproj`에 11번 항목을 추가한다.
5. `dotnet build RideMateService.sln -warnaserror`가 경고 0으로 통과하도록 고친다. 세 번 고쳐도 통과하지 못하면 멈추고 남은 오류를 사용자에게 알린다.
6. 서버를 띄워 Swagger에서 새 API를 호출하고 응답을 확인한다. 로컬 서버와 로컬 DB가 대상일 때만 쓰기 API도 호출한다. 서버를 띄우고 끄는 방법은 `backend/AGENTS.md` §빌드·실행·검증을 따른다. `sign` 구간 API는 `docs/security.md`의 방법으로 받은 access 토큰을 Bearer로 실어 호출한다.
7. `update-api-spec` 스킬로 `docs/api/openapi.json`을 갱신하고 Notion API 문서(정본)에 새 API 페이지를 쓴다. Notion에 연결되지 않은 실행 환경이면 `openapi.json`까지만 하고, 써야 할 Notion 페이지를 결과 보고에 남긴다(Notion은 연결된 세션이 쓴다).
8. `feat: <한국어 요약>`으로 커밋한다.

## 완료 조건

아래를 모두 확인한 뒤에 끝났다고 보고한다. 하나라도 안 되면 해당 절차로 돌아간다.

- [ ] 표의 파일이 그 위치와 이름으로 있다.
- [ ] 자리표시자가 남아 있지 않다. 아래 명령의 출력이 없다.
      `grep -rnE '\{(도메인|엔티티|테이블|한글|PK\})' RideMateService --include='*.cs' --include='*.csproj'`
- [ ] `dotnet build RideMateService.sln -warnaserror`가 경고 0, 오류 0이다.
- [ ] 새 API의 응답이 `{ message, data, code }` 형태이고, `code`와 HTTP 상태가 `api.md` 표와 맞다.
- [ ] 새로 만든 파일이 커밋에 들어 있다. `git show --stat --format= HEAD`에 전부 나오고 `git check-ignore -v <파일>`에 걸리지 않는다(도메인 이름이 `Log`·`Debug`·`Release`·`Publish`처럼 `backend/.gitignore`가 무시하는 폴더 이름이면 통째로 빠진다).
- [ ] `docs/api/openapi.json`에 새 라우트가 들어 있다. Notion "API 상세" 목록 표와 하위 페이지가 있거나(연결된 세션), 남긴 Notion 작업이 결과 보고에 있다(연결되지 않은 환경).
- [ ] 변경이 커밋되어 `git status --short`가 비어 있다.
