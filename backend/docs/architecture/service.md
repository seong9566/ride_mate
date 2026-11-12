# Service 레이어

위치: `Service/v1/{도메인}/I{도메인}Service.cs`, `{도메인}Service.cs`.

템플릿: `.agents/skills/add-domain/templates/IService.cs`, `.agents/skills/add-domain/templates/Service.cs`. 새 Service와 메서드는 이 템플릿의 구조를 그대로 쓴다.

- 인증 확인·입력 검증·엔티티 매핑·분기·트랜잭션은 모두 Service에 둔다.
- 반환은 항상 `ResponseModel<T>`다. 예외를 Controller로 던지지 않고 `code = 500`으로 바꿔 돌려준다. `code` 값과 `message` 문구는 `../api.md`의 표만 쓴다.
  - 예외: HTTP 요청 밖에서 백그라운드 워커가 부르는 메서드는 `ResponseModel<T>`를 쓰지 않는다. 만료 주기 작업의 `IPickupService.ExpirePickupPlansService`는 처리한 건수(`Task<int>`)를 돌려주고 `HttpContext`를 쓰지 않는다. 항목 하나의 오류는 로그만 남기고 다음 항목으로 가며, 그 밖의 예외는 워커가 회차 단위로 받아 로그만 남긴다(`overview.md` "DI 등록").
- 메서드마다 try/catch를 둔다. 전역 예외 필터로 바꾸지 않는다. [ADR-0004]
- 모든 메서드는 마지막 파라미터로 `CancellationToken cancellationToken = default`를 받아 Repository에 넘긴다. 요청이 취소된 경우에만 예외를 500으로 바꾸지 않고 그대로 올려보낸다(템플릿 catch의 `when` 조건).
- DTO → 엔티티 변환은 Service에서 한다. Repository에 DTO를 넘기지 않는다.
- 감사 컬럼(`CreateDt`·`CreateUser`·`UpdateDt`·`UpdateUser`·`DeleteYn`)은 Service가 채운다. Repository나 DB 트리거에 맡기지 않는다.
- 로그인 사용자는 `IHttpContextAccessor`의 클레임에서 꺼낸다. Controller 파라미터로 받지 않는다.
- 시각은 `DateTime.Now`를 쓴다(서버 `TZ=Asia/Seoul` 전제). 한 메서드 안에서는 `thisTime` 하나를 재사용한다.
- 여러 테이블을 함께 바꾸면 `_context.Database.BeginTransactionAsync()`로 묶는다.
- Service가 주입받은 `RideMateContext`는 트랜잭션을 시작·커밋·롤백하는 데만 쓴다. 조회와 저장은 Repository를 거친다.
