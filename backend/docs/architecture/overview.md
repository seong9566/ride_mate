# 아키텍처 개요

`{도메인}`은 PascalCase 단수(예: `Pickup`), `{도메인소문자}`는 camelCase(예: `pickup`)다. 경로는 `RideMateService/RideMateService/` 기준이다.

## 폴더 구조

폴더는 `{레이어}/v1/{도메인}/`으로 맞춘다. 한 도메인을 네 레이어에서 같은 경로로 찾을 수 있게 한다.

```
RideMateService/
├── Program.cs
├── Comm/                         # 공통 계약·횡단 관심사·DTO
│   ├── ResponseModel.cs          # 모든 Service 반환 타입
│   ├── CursorResult.cs           # 커서 페이징 결과
│   ├── RunActionFilter.cs        # 액션 실행 시간 로깅 필터
│   ├── DateOnlyHandler.cs        # Dapper DateOnly 핸들러
│   ├── Logger/                   # ILogService, LogService
│   └── Dto/v1/{도메인}/Request|Response/
├── Commons/
│   ├── DBModels/                 # 스캐폴드 산출물 (직접 수정 금지)
│   └── Repository/
│       ├── RideMateContext.cs            # 스캐폴드 산출물 (직접 수정 금지)
│       └── RideMateContext.Extensions.cs # 손으로 쓰는 partial
├── Controller/
│   ├── ApiControllerBase.cs
│   └── v1/{도메인}/{도메인}Controller.cs
├── Service/v1/{도메인}/          # I{도메인}Service, {도메인}Service
├── Repository/v1/{도메인}/       # I{도메인}Repository + 3분할
└── Extensions/                   # DI 등록 확장 메서드 (첫 도메인 추가 때 만든다)
```

레이어별 세부는 같은 폴더의 `controller.md`·`service.md`·`repository.md`·`dto.md`·`database.md`를 본다.

## 계층 의존 방향

```
Controller ──▶ Service ──▶ Repository ──▶ DbContext / IDbConnection
     └────────────┴─────────────┴──▶ Comm (ResponseModel, Logger, …)
```

- 역방향 참조를 하지 않는다. Repository가 Service를 알면 안 된다.
- Controller는 Repository를 직접 호출하지 않는다. 반드시 Service를 거친다.
- 레이어를 건너뛰지 않는다. 단순 CRUD도 Service를 만든다.

## 도메인 추가 순서

새 도메인은 DTO → Repository → Service → Controller → DI 등록 → csproj 등록 순서로 만든다. 만들 파일의 위치·이름·템플릿과 절차는 `add-domain` 스킬(`.agents/skills/add-domain/SKILL.md`)의 "만들 파일" 표를 따른다.

## Program.cs 구성 순서

`#region`으로 블록을 나누고, 서비스 등록 → `Build()` → 미들웨어 순서를 지킨다. 아직 없는 블록은 추가할 때 이 순서의 자리에 넣는다.

서비스 등록 순서:

1. Kestrel 설정
2. ForwardedHeaders (프록시 뒤 클라이언트 IP·스킴 복원)
3. 응답 압축 (Brotli + Gzip)
4. Redis (`IConnectionMultiplexer` Singleton)
5. Swagger (Bearer 보안 정의 포함)
6. 도메인 DI (`AddDomainModules()`)
7. 백그라운드 워커 (HostedService)
8. 공용 서비스 (`AddCommonInfrastructure()` — `ILogService`, HttpContextAccessor)
9. Dapper 타입 핸들러
10. 인증
11. DbContext + Dapper `IDbConnection` (연결 문자열이 없으면 기동 실패)
12. CORS (허용 오리진이 없으면 기동 실패)
13. `AddControllers` + `RunActionFilter` + `AddNewtonsoftJson`
14. `AddAuthorization`

미들웨어 순서 — 바꾸면 조용히 오작동한다:

1. `UseForwardedHeaders()` — 반드시 맨 앞
2. `UseResponseCompression()`
3. `UseRouting()`
4. `UseSwagger()` / `UseSwaggerUI()`
5. `UseStaticFiles()`
6. `UseCors()`
7. 인증 구간 미들웨어
8. `UseAuthentication()`
9. `UseAuthorization()`
10. `MapGet("/")` → `/swagger` 리다이렉트, `MapControllers()`

- 꺼져 있어도 에러가 나지 않는 기능(스케줄러·워커)은 ON/OFF 상태를 기동 로그에 남긴다.

## DI 등록

| 수명 | 대상 |
|---|---|
| Singleton | `IConnectionMultiplexer`, 서명·암호 키를 들고 있는 서비스 |
| Transient | Repository, Service, `ILogService`, `IDbConnection` |
| Scoped | `DbContext` (`AddDbContext` 기본값) |

- Singleton에 Scoped를 주입하지 않는다. 필요하면 `IServiceScopeFactory`로 스코프를 열어 쓴다.
- 도메인 등록은 `Extensions/ServiceCollectionExtensions.cs`의 확장 메서드로 모은다. `Program.cs`는 `builder.Services.AddDomainModules();` 한 줄만 갖는다. 템플릿: `.agents/skills/add-domain/templates/ServiceCollectionExtensions.cs`.
- 공용 서비스(`ILogService` 등)는 같은 폴더의 `AddCommonInfrastructure()`로 모은다. 첫 도메인을 추가할 때 `Extensions/`를 만들고 `Program.cs`에 있는 `ILogService` 등록도 이쪽으로 옮긴다.
- 어셈블리 스캔(Scrutor 등)으로 자동 등록하지 않는다. 등록은 모두 코드에 명시한다.
