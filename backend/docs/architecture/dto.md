# DTO

위치: `Comm/Dto/v1/{도메인}/Request/`(요청), `Comm/Dto/v1/{도메인}/Response/`(응답).

## 파일 이름

| 종류 | 형식 | 예 |
|---|---|---|
| 요청 | `{동작}{도메인}Dto.cs` | `AddPickupDto`, `UpdatePickupDto` |
| 목록 응답 | `Get{도메인}List.cs` | `GetPickupList` |
| 상세 응답 | `Detail{도메인}Dto.cs` | `DetailPickupDto` |

## 프로퍼티

템플릿: `.agents/skills/add-domain/templates/RequestDto.cs`, `.agents/skills/add-domain/templates/ListResponseDto.cs`.

- 프로퍼티는 camelCase로 쓴다. JSON 키와 1:1로 맞춘다. 대상은 `Comm/Dto/**`·`ResponseModel`·`CursorResult`다. 스캐폴드 엔티티(`Commons/DBModels`)는 PascalCase 그대로 둔다.
- PK는 `{도메인}Seq`로 노출한다. 컬럼이 `PICKUP_ID`여도 `pickupSeq`, 커서는 `lastPickupSeq`다.
- 불리언은 `Yn` 접미에 `bool` 타입을 쓴다(`useYn`, `deleteYn`). `"Y"`·`"N"` 문자열을 쓰지 않는다.
- 요청 DTO에 검증 특성을 붙인다. 필수 문자열은 `[Required]`, 길이는 DB 컬럼 길이에 맞춘 `[StringLength]`, 숫자 범위는 `[Range]`를 쓴다. 검증에 실패하면 `[ApiController]`가 액션에 들어가기 전에 400을 돌려준다(`../api.md`).
- 값 타입(`int`·`bool`)은 요청에서 빠져도 기본값으로 들어온다. 빠진 것을 막아야 하는 필드는 nullable로 선언하고 `[Required]`를 붙인다.
- 수정(PATCH) 요청 DTO는 PK를 뺀 필드를 모두 nullable로 선언한다. `null`이거나 빠진 필드는 "변경하지 않음"이다. Service는 `null`이 아닌 필드만 엔티티에 반영한다.
  - 예외: `UpdateScheduleDto`는 시간표 전체를 바꾸므로 `AddScheduleDto`를 상속해 필드가 모두 필수다(`../api.md` "시간표 편집").
- 예외: `Token`의 요청 DTO(`TokenAuthDto`)와 응답 DTO(`TokenAuthSuccessDto`, `TokenAuthErrorDto`)는 OAuth 2.0 필드 이름 그대로 snake_case로 쓴다. 요청 DTO는 모든 필드를 nullable로 두며 검증 특성을 붙이지 않는다. 붙이면 값이 빠졌을 때 `[ApiController]`가 먼저 `ProblemDetails` 400을 돌려줘 `invalid_request`가 나가지 않는다. 검증은 Service에서 한다(`../api.md`).
- 엔티티를 응답으로 그대로 내보내지 않는다. 항상 응답 DTO로 옮긴다.
- 프로퍼티 이름을 바꾸면 앱 파싱이 깨진다. 이미 배포한 DTO의 이름을 바꿀 때는 API 명세 갱신과 함께 모빌리티 담당에게 알린다.
