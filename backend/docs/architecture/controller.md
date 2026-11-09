# Controller 레이어

위치: `Controller/v1/{도메인}/{도메인}Controller.cs`. 모든 컨트롤러는 `ApiControllerBase`를 상속한다.

템플릿: `.agents/skills/add-domain/templates/Controller.cs`. 새 컨트롤러와 액션은 이 템플릿의 구조를 그대로 쓴다.

- 액션 본문은 Service 호출 1줄과 `ToActionResult` 1줄뿐이다. 로직·분기·매핑을 두지 않는다.
- 모든 액션을 템플릿 형태의 try/catch로 감싼다. catch 안 문구도 그대로 쓴다.
- 모든 액션은 마지막 파라미터로 `CancellationToken cancellationToken`을 받아 Service에 넘긴다. 요청이 취소됐으면 오류로 기록하지 않고 499를 돌려준다(템플릿의 첫 번째 catch).
- `[Produces("application/json")]`와 한글 `[SwaggerOperation(Summary = ...)]`를 반드시 붙인다.
- 클래스에는 `[Route("api")]`만 두고, 버전·도메인은 액션의 `[Route]`에 적는다. 라우트 형태와 액션 이름은 `../api.md`를 따른다.
- 쿼리스트링은 `[FromQuery]`, 필수면 `[Required]`를 붙인다.
- `ResponseModel.code` → HTTP 상태 변환은 `ApiControllerBase.ToActionResult()` 한 곳에서만 한다. 새 `code` 값을 쓰면 그 switch에 추가한다.
