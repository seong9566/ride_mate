# Service 레이어

위치: `Service/v1/{도메인}/I{도메인}Service.cs`, `{도메인}Service.cs`.

템플릿: `.agents/skills/add-domain/templates/IService.cs`, `.agents/skills/add-domain/templates/Service.cs`. 새 Service와 메서드는 이 템플릿의 구조를 그대로 쓴다.

- 인증 확인·입력 검증·엔티티 매핑·분기·트랜잭션은 모두 Service에 둔다.
- 반환은 항상 `ResponseModel<T>`다. 예외를 Controller로 던지지 않고 `code = 500`으로 바꿔 돌려준다. `code` 값과 `message` 문구는 `../api.md`의 표만 쓴다.
  - 예외: HTTP 요청 밖에서 백그라운드 워커가 부르는 메서드는 `ResponseModel<T>`를 쓰지 않는다. 만료 주기 작업의 `IPickupService.ExpirePickupPlansService`는 처리한 건수(`Task<int>`)를 돌려주고 `HttpContext`를 쓰지 않는다. 인증번호 행 정리 주기 작업의 `IAuthService.CleanupPhoneVerificationsService`도 같다(정리한 행 수를 돌려주고, 묶음 하나의 오류는 로그만 남기고 그 회차를 끝낸다). 항목 하나의 오류는 로그만 남기고 다음 항목으로 가며, 그 밖의 예외는 워커가 회차 단위로 받아 로그만 남긴다(`overview.md` "DI 등록").
  - 예외: SSE 스트림 읽기 메서드(`IPickupService.ReadChildPickupStreamAsync`·`ReadPickupLinkStreamAsync`)는 `IAsyncEnumerable`을 돌려준다. 연결 판정(토큰·404)은 짝이 되는 `Open…Service`가 `ResponseModel<T>`로 먼저 돌려주고, 판정을 통과한 뒤의 스트림에서 오류가 나면 로그만 남기고 스트림을 끝낸다(예외를 밖으로 던지지 않는다. 요청 취소는 로그 없이 끝낸다). [ADR-0024]
- 계획 행(`PICKUP_PLAN`)이나 `PICKUP_EVENT`를 바꾸는 트랜잭션은 커밋한 직후 `SignalPlanChanged(planId)`를 부르고, 계획을 끝내는(탑승·취소·만료) 커밋 뒤에는 `SignalPlanEnded(planId)`를 부른다. 롤백·"바꿀 것 없음" 경로에서는 부르지 않는다. 아이 화면 실시간 연결이 이 신호로 다시 읽기 때문에, 빠뜨리면 그 변화가 재연결 전까지 보이지 않는다. 지금 계획을 끝내는 경로는 Pickup 서비스 안에만 있다. 아이·계정 삭제처럼 계획을 끝내는 API를 새로 만들면 그 커밋 뒤에도 `IPickupLiveHub.NotifyPlanEnded`를 부른다. [ADR-0024]
- 메서드마다 try/catch를 둔다. 전역 예외 필터로 바꾸지 않는다. [ADR-0004]
- 모든 메서드는 마지막 파라미터로 `CancellationToken cancellationToken = default`를 받아 Repository에 넘긴다. 요청이 취소된 경우에만 예외를 500으로 바꾸지 않고 그대로 올려보낸다(템플릿 catch의 `when` 조건).
- DTO → 엔티티 변환은 Service에서 한다. Repository에 DTO를 넘기지 않는다.
- 감사 컬럼(`CreateDt`·`CreateUser`·`UpdateDt`·`UpdateUser`·`DeleteYn`)은 Service가 채운다. Repository나 DB 트리거에 맡기지 않는다.
- 로그인 사용자는 `IHttpContextAccessor`의 클레임에서 꺼낸다. Controller 파라미터로 받지 않는다.
- 시각은 `DateTime.Now`를 쓴다(서버 `TZ=Asia/Seoul` 전제). 한 메서드 안에서는 `thisTime` 하나를 재사용한다.
- 여러 테이블을 함께 바꾸면 `_context.Database.BeginTransactionAsync()`로 묶는다.
- Service가 주입받은 `RideMateContext`는 트랜잭션을 시작·커밋·롤백하는 데만 쓴다. 조회와 저장은 Repository를 거친다.
