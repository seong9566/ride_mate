using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using RideMateService.Comm;
using RideMateService.Comm.Dto.v1.Pickup.Request;
using RideMateService.Comm.Dto.v1.Pickup.Response;
using RideMateService.Comm.Logger;
using RideMateService.Service.v1.Pickup;
using Swashbuckle.AspNetCore.Annotations;

namespace RideMateService.Controller.v1.Pickup;

[Route("api")]
[ApiController]
public class PickupController : ApiControllerBase
{
    private readonly ILogService _logService;
    private readonly IPickupService _pickupService;
    private readonly JsonSerializerSettings _jsonSettings;

    public PickupController(ILogService logService, IPickupService pickupService,
        IOptions<MvcNewtonsoftJsonOptions> jsonOptions)
    {
        this._logService = logService;
        this._pickupService = pickupService;
        this._jsonSettings = jsonOptions.Value.SerializerSettings;
    }

    /// <summary>
    /// 출발 안내 계산
    /// </summary>
    /// <remarks>
    /// 요청: scheduleSeq(시간표 ID)·dayOfWeek(1월~7일)·currentLat·currentLng 필수, pickupSpotSeq 또는 pickupLat·pickupLng 선택(함께 보낼 수 없음).
    /// 응답 code: 101 dayOfWeek가 서버의 오늘 요일이 아니거나 그 일정의 오늘 픽업 없음, 200 정상, 400 잘못된 입력, 401 인증 실패·계정 없음, 500 서버 처리 실패
    /// </remarks>
    [Produces("application/json")]
    [HttpGet]
    [Route("v1/Pickup/M/sign/DetailDepartureGuide")]
    [SwaggerOperation(Summary = "출발 안내 계산")]
    [ProducesResponseType(typeof(ResponseModel<DetailDepartureGuideDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseModel<DetailDepartureGuideDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseModel<DetailDepartureGuideDto>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DetailDepartureGuide([FromQuery] DepartureGuideQueryDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            var model = await _pickupService.DetailDepartureGuideService(dto, cancellationToken).ConfigureAwait(false);
            return ToActionResult(model);
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
            // 앱이 요청을 끊었다. 오류로 기록하지 않는다
            return StatusCode(StatusCodes.Status499ClientClosedRequest);
        }
        catch (Exception ex)
        {
            _logService.LogMessage(ex.ToString());
#if DEBUG
            _logService.ConsoleWarning(ex.ToString());
#endif
            return Problem("서버에서 요청을 처리하지 못하였습니다. 잠시후 다시 시도해주세요.", statusCode: 500);
        }
    }

    /// <summary>
    /// 오늘의 픽업 경로 조회
    /// </summary>
    /// <remarks>
    /// 현재 위치 → 학원 → (경유지) → 집 시간을 서버가 카카오모빌리티 자동차 길찾기(지금 출발) 한 번으로 구한다.
    /// 경유지(최대 4개)를 query 대신 JSON 본문으로 받으므로 조회지만 POST다.
    /// 응답 code: 101 오늘 그 시간표의 픽업 없음(오늘 요일이 아님·스위치 꺼짐·오늘 쉼·이미 마침·없거나 남의 시간표), 200 정상,
    /// 400 잘못된 입력·경유지 주소를 찾을 수 없음, 401 인증 실패, 500 서버 처리 실패
    /// </remarks>
    [Consumes("application/json")]
    [Produces("application/json")]
    [HttpPost]
    [Route("v1/Pickup/M/sign/DetailTodayPickupRoute")]
    [SwaggerOperation(Summary = "오늘의 픽업 경로 조회")]
    [ProducesResponseType(typeof(ResponseModel<DetailTodayPickupRouteDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseModel<DetailTodayPickupRouteDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseModel<DetailTodayPickupRouteDto>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DetailTodayPickupRoute([FromBody] DetailTodayPickupRouteReqDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            var model = await _pickupService.DetailTodayPickupRouteService(dto, cancellationToken).ConfigureAwait(false);
            return ToActionResult(model);
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
            // 앱이 요청을 끊었다. 오류로 기록하지 않는다
            return StatusCode(StatusCodes.Status499ClientClosedRequest);
        }
        catch (Exception ex)
        {
            _logService.LogMessage(ex.ToString());
#if DEBUG
            _logService.ConsoleWarning(ex.ToString());
#endif
            return Problem("서버에서 요청을 처리하지 못하였습니다. 잠시후 다시 시도해주세요.", statusCode: 500);
        }
    }

    /// <summary>
    /// 대기 장소 추천 목록
    /// </summary>
    /// <remarks>
    /// 요청은 출발 안내와 같다: scheduleSeq(시간표 ID)·dayOfWeek(1월~7일)·currentLat·currentLng 필수, pickupSpotSeq 또는 pickupLat·pickupLng 선택(함께 보낼 수 없음).
    /// 응답 code: 101 dayOfWeek가 서버의 오늘 요일이 아니거나 그 일정의 오늘 픽업 없음, 200 정상, 400 잘못된 입력, 401 인증 실패·계정 없음, 500 서버 처리 실패
    /// </remarks>
    [Produces("application/json")]
    [HttpGet]
    [Route("v1/Pickup/M/sign/GetWaitingPlaceList")]
    [SwaggerOperation(Summary = "대기 장소 추천 목록")]
    [ProducesResponseType(typeof(ResponseModel<WaitingPlaceListDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseModel<WaitingPlaceListDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseModel<WaitingPlaceListDto>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetWaitingPlaceList([FromQuery] WaitingPlaceQueryDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            var model = await _pickupService.GetWaitingPlaceListService(dto, cancellationToken).ConfigureAwait(false);
            return ToActionResult(model);
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
            // 앱이 요청을 끊었다. 오류로 기록하지 않는다
            return StatusCode(StatusCodes.Status499ClientClosedRequest);
        }
        catch (Exception ex)
        {
            _logService.LogMessage(ex.ToString());
#if DEBUG
            _logService.ConsoleWarning(ex.ToString());
#endif
            return Problem("서버에서 요청을 처리하지 못하였습니다. 잠시후 다시 시도해주세요.", statusCode: 500);
        }
    }

    /// <summary>
    /// 픽업 장소 후보 목록
    /// </summary>
    /// <remarks>
    /// 응답 code: 200 정상(후보가 없으면 reason과 빈 목록), 400 잘못된 입력, 401 인증 실패, 404 시간표 없음, 500 서버 처리 실패
    /// </remarks>
    [Produces("application/json")]
    [HttpGet]
    [Route("v1/Pickup/M/sign/GetPickupSpotList")]
    [SwaggerOperation(Summary = "픽업 장소 후보 목록")]
    [ProducesResponseType(typeof(ResponseModel<PickupSpotListDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseModel<PickupSpotListDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseModel<PickupSpotListDto>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseModel<PickupSpotListDto>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetPickupSpotList([FromQuery] PickupSpotListQueryDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            var model = await _pickupService.GetPickupSpotListService(dto, cancellationToken).ConfigureAwait(false);
            return ToActionResult(model);
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
            // 앱이 요청을 끊었다. 오류로 기록하지 않는다
            return StatusCode(StatusCodes.Status499ClientClosedRequest);
        }
        catch (Exception ex)
        {
            _logService.LogMessage(ex.ToString());
#if DEBUG
            _logService.ConsoleWarning(ex.ToString());
#endif
            return Problem("서버에서 요청을 처리하지 못하였습니다. 잠시후 다시 시도해주세요.", statusCode: 500);
        }
    }

    /// <summary>
    /// 학원가 단속 구역 목록
    /// </summary>
    [Produces("application/json")]
    [HttpGet]
    [Route("v1/Pickup/M/sign/GetRestrictedZoneList")]
    [SwaggerOperation(Summary = "학원가 단속 구역 목록")]
    [ProducesResponseType(typeof(ResponseModel<GetRestrictedZoneList>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRestrictedZoneList([FromQuery] RestrictedZoneListQueryDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            var model = await _pickupService.GetRestrictedZoneListService(dto, cancellationToken).ConfigureAwait(false);
            return ToActionResult(model);
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
            // 앱이 요청을 끊었다. 오류로 기록하지 않는다
            return StatusCode(StatusCodes.Status499ClientClosedRequest);
        }
        catch (Exception ex)
        {
            _logService.LogMessage(ex.ToString());
#if DEBUG
            _logService.ConsoleWarning(ex.ToString());
#endif
            return Problem("서버에서 요청을 처리하지 못하였습니다. 잠시후 다시 시도해주세요.", statusCode: 500);
        }
    }

    /// <summary>
    /// 픽업 링크 문자 발송
    /// </summary>
    [Produces("application/json")]
    [HttpPost]
    [Route("v1/Pickup/M/sign/AddPickupLink")]
    [SwaggerOperation(Summary = "픽업 링크 문자 발송")]
    [ProducesResponseType(typeof(ResponseModel<PickupLinkSendDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> AddPickupLink([FromBody] AddPickupLinkDto dto, CancellationToken cancellationToken)
    {
        try
        {
            var model = await _pickupService.AddPickupLinkService(dto, cancellationToken).ConfigureAwait(false);
            return ToActionResult(model);
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
            // 앱이 요청을 끊었다. 오류로 기록하지 않는다
            return StatusCode(StatusCodes.Status499ClientClosedRequest);
        }
        catch (Exception ex)
        {
            _logService.LogMessage(ex.ToString());
#if DEBUG
            _logService.ConsoleWarning(ex.ToString());
#endif
            return Problem("서버에서 요청을 처리하지 못하였습니다. 잠시후 다시 시도해주세요.", statusCode: 500);
        }
    }

    /// <summary>
    /// 정차 전 픽업 링크 다시 보내기
    /// </summary>
    [Produces("application/json")]
    [HttpPost]
    [Route("v1/Pickup/M/sign/AddPickupLinkResend")]
    [SwaggerOperation(Summary = "정차 전 픽업 링크 다시 보내기")]
    [ProducesResponseType(typeof(ResponseModel<PickupLinkSendDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> AddPickupLinkResend([FromBody] AddPickupLinkResendDto dto, CancellationToken cancellationToken)
    {
        try
        {
            var model = await _pickupService.AddPickupLinkResendService(dto, cancellationToken).ConfigureAwait(false);
            return ToActionResult(model);
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
            // 앱이 요청을 끊었다. 오류로 기록하지 않는다
            return StatusCode(StatusCodes.Status499ClientClosedRequest);
        }
        catch (Exception ex)
        {
            _logService.LogMessage(ex.ToString());
#if DEBUG
            _logService.ConsoleWarning(ex.ToString());
#endif
            return Problem("서버에서 요청을 처리하지 못하였습니다. 잠시후 다시 시도해주세요.", statusCode: 500);
        }
    }

    /// <summary>
    /// 정차 후 다시 알림 문자 발송
    /// </summary>
    [Produces("application/json")]
    [HttpPost]
    [Route("v1/Pickup/M/sign/AddPickupRemind")]
    [SwaggerOperation(Summary = "정차 후 다시 알림 문자 발송")]
    [ProducesResponseType(typeof(ResponseModel<PickupLinkSendDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> AddPickupRemind([FromBody] AddPickupRemindDto dto, CancellationToken cancellationToken)
    {
        try
        {
            var model = await _pickupService.AddPickupRemindService(dto, cancellationToken).ConfigureAwait(false);
            return ToActionResult(model);
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
            // 앱이 요청을 끊었다. 오류로 기록하지 않는다
            return StatusCode(StatusCodes.Status499ClientClosedRequest);
        }
        catch (Exception ex)
        {
            _logService.LogMessage(ex.ToString());
#if DEBUG
            _logService.ConsoleWarning(ex.ToString());
#endif
            return Problem("서버에서 요청을 처리하지 못하였습니다. 잠시후 다시 시도해주세요.", statusCode: 500);
        }
    }

    /// <summary>
    /// 픽업 링크 전달 상태 조회
    /// </summary>
    [Produces("application/json")]
    [HttpGet]
    [Route("v1/Pickup/M/sign/DetailPickupLinkStatus")]
    [SwaggerOperation(Summary = "픽업 링크 전달 상태 조회")]
    [ProducesResponseType(typeof(ResponseModel<DetailPickupLinkStatusDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DetailPickupLinkStatus([FromQuery] PickupLinkStatusQueryDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            var model = await _pickupService.DetailPickupLinkStatusService(dto, cancellationToken).ConfigureAwait(false);
            return ToActionResult(model);
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
            // 앱이 요청을 끊었다. 오류로 기록하지 않는다
            return StatusCode(StatusCodes.Status499ClientClosedRequest);
        }
        catch (Exception ex)
        {
            _logService.LogMessage(ex.ToString());
#if DEBUG
            _logService.ConsoleWarning(ex.ToString());
#endif
            return Problem("서버에서 요청을 처리하지 못하였습니다. 잠시후 다시 시도해주세요.", statusCode: 500);
        }
    }

    /// <summary>
    /// 픽업 링크 전달 상태 실시간 연결(SSE)
    /// </summary>
    /// <remarks>
    /// 판정 실패는 SSE가 아니라 JSON이다: 400 pickupPlanSeq 없음, 401 인증 실패, 404 계획 없음·남의 계획·끝난 계획, 429 요청 초과(사용자마다 1분 20회), 500.
    /// 성공하면 text/event-stream으로 연결 직후 `linkStatus`(DetailPickupLinkStatus의 data와 같은 모양) 하나를 보내고, 아이 열람·출발 알림·"곧 나와요" 전환·문자 발송 결과로 값이 바뀔 때만 다시 보낸다. 15초마다 `: ping` 주석 줄.
    /// 계획이 끝나면(만료 시각 포함) `closed`를 보내고 닫는다. 접근 토큰 만료 시각, 30분, 같은 계획의 차량 연결 5개 초과(가장 오래된 것)에도 서버가 닫는다. 기존 DetailPickupLinkStatus 조회는 그대로 쓸 수 있다.
    /// </remarks>
    [HttpGet]
    [Route("v1/Pickup/M/sign/DetailPickupLinkStream")]
    [EnableRateLimiting(PickupLinkStreamRateLimit.PolicyName)]
    [SwaggerOperation(Summary = "픽업 링크 전달 상태 실시간 연결(SSE)")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseModel<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DetailPickupLinkStream([FromQuery] PickupLinkStatusQueryDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            var model = await _pickupService.OpenPickupLinkStreamService(dto, cancellationToken).ConfigureAwait(false);
            if (model.code != 200)
                return ToActionResult(model);

            await SseWriter.WriteAsync(Response, _pickupService.ReadPickupLinkStreamAsync(model.data!, cancellationToken),
                _jsonSettings, cancellationToken).ConfigureAwait(false);
            return new EmptyResult();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new EmptyResult();
        }
        catch (Exception ex)
        {
            var message = $"vehicle link stream failed: plan={dto?.pickupPlanSeq?.ToString() ?? "unknown"}\n{ex}";
            _logService.LogMessage(message);
#if DEBUG
            _logService.ConsoleWarning(message);
#endif
            if (Response.HasStarted)
                return new EmptyResult();

            Response.ContentType = null;
            return Problem("서버에서 요청을 처리하지 못하였습니다. 잠시후 다시 시도해주세요.", statusCode: 500);
        }
    }

    /// <summary>
    /// 픽업 장소 바꾸기(임의 좌표로 바꾸거나 기본 지점으로 되돌리기)
    /// </summary>
    [Produces("application/json")]
    [HttpPatch]
    [Route("v1/Pickup/M/sign/UpdatePickupPlanLocation")]
    [SwaggerOperation(Summary = "픽업 장소 바꾸기")]
    [ProducesResponseType(typeof(ResponseModel<PickupPlanDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdatePickupPlanLocation([FromBody] UpdatePickupPlanLocationDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            var model = await _pickupService.UpdatePickupPlanLocationService(dto, cancellationToken).ConfigureAwait(false);
            return ToActionResult(model);
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
            // 앱이 요청을 끊었다. 오류로 기록하지 않는다
            return StatusCode(StatusCodes.Status499ClientClosedRequest);
        }
        catch (Exception ex)
        {
            _logService.LogMessage(ex.ToString());
#if DEBUG
            _logService.ConsoleWarning(ex.ToString());
#endif
            return Problem("서버에서 요청을 처리하지 못하였습니다. 잠시후 다시 시도해주세요.", statusCode: 500);
        }
    }

    /// <summary>
    /// 경로 시작(scheduleSeq로 지정한 일정의 오늘 계획이 없으면 만들면서 시작하고, 늦게 출발했으면 같은 장소에서 배정 시각을 미룬다)
    /// </summary>
    [Produces("application/json")]
    [HttpPatch]
    [Route("v1/Pickup/M/sign/UpdatePickupPlanStart")]
    [SwaggerOperation(Summary = "경로 시작")]
    [ProducesResponseType(typeof(ResponseModel<PickupPlanDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseModel<PickupPlanDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseModel<PickupPlanDto>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseModel<PickupPlanDto>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseModel<PickupPlanDto>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdatePickupPlanStart([FromBody] UpdatePickupPlanStartDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            var model = await _pickupService.UpdatePickupPlanStartService(dto, cancellationToken).ConfigureAwait(false);
            return ToActionResult(model);
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
            // 앱이 요청을 끊었다. 오류로 기록하지 않는다
            return StatusCode(StatusCodes.Status499ClientClosedRequest);
        }
        catch (Exception ex)
        {
            _logService.LogMessage(ex.ToString());
#if DEBUG
            _logService.ConsoleWarning(ex.ToString());
#endif
            return Problem("서버에서 요청을 처리하지 못하였습니다. 잠시후 다시 시도해주세요.", statusCode: 500);
        }
    }

    /// <summary>
    /// 정차 기록(배정 장소 반경과 정차 위치의 단속 구역을 판정한다)
    /// </summary>
    [Produces("application/json")]
    [HttpPatch]
    [Route("v1/Pickup/M/sign/UpdatePickupPlanStop")]
    [SwaggerOperation(Summary = "정차 기록")]
    [ProducesResponseType(typeof(ResponseModel<PickupPlanDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdatePickupPlanStop([FromBody] UpdatePickupPlanStopDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            var model = await _pickupService.UpdatePickupPlanStopService(dto, cancellationToken).ConfigureAwait(false);
            return ToActionResult(model);
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
            // 앱이 요청을 끊었다. 오류로 기록하지 않는다
            return StatusCode(StatusCodes.Status499ClientClosedRequest);
        }
        catch (Exception ex)
        {
            _logService.LogMessage(ex.ToString());
#if DEBUG
            _logService.ConsoleWarning(ex.ToString());
#endif
            return Problem("서버에서 요청을 처리하지 못하였습니다. 잠시후 다시 시도해주세요.", statusCode: 500);
        }
    }

    /// <summary>차 위치·내비 남은 시간 보고</summary>
    /// <remarks>경로 시작부터 탑승 전까지 3초마다 부른다. etaSec는 내비 목적지가 이 계획의 픽업 장소일 때만 보낸다. 위치는 서버 메모리에 계획마다 마지막 하나만 두고 DB에 쓰지 않는다. 오프라인 대기열에 쌓아 다시 보내지 않는다. 1초 안에 다시 온 보고는 200으로 받고 버린다.
    /// 응답 code: 200 정상(data null), 400 검증 실패, 401 인증 실패, 404 계획 없음·남의 계획·끝난 계획, 409 PLAN_CLOSED·NOT_STARTED, 429 요청 초과(사용자마다 1분 120회), 500 서버 처리 실패</remarks>
    [Produces("application/json")]
    [HttpPatch]
    [Route("v1/Pickup/M/sign/UpdatePickupPlanPosition")]
    [Consumes("application/json")]
    [EnableRateLimiting(PickupPositionRateLimit.PolicyName)]
    [SwaggerOperation(Summary = "차 위치·내비 남은 시간 보고")]
    [ProducesResponseType(typeof(ResponseModel<PickupPlanPositionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseModel<PickupPlanPositionDto>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseModel<PickupPlanPositionDto>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(void), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdatePickupPlanPosition([FromBody] UpdatePickupPlanPositionDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            var model = await _pickupService.UpdatePickupPlanPositionService(dto, cancellationToken).ConfigureAwait(false);
            return ToActionResult(model);
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
            // 앱이 요청을 끊었다. 오류로 기록하지 않는다
            return StatusCode(StatusCodes.Status499ClientClosedRequest);
        }
        catch (Exception ex)
        {
            _logService.LogMessage(ex.ToString());
#if DEBUG
            _logService.ConsoleWarning(ex.ToString());
#endif
            return Problem("서버에서 요청을 처리하지 못하였습니다. 잠시후 다시 시도해주세요.", statusCode: 500);
        }
    }

    /// <summary>
    /// 탑승 기록(오늘 계획을 BOARDED로 끝낸다)
    /// </summary>
    [Produces("application/json")]
    [HttpPatch]
    [Route("v1/Pickup/M/sign/UpdatePickupPlanBoard")]
    [SwaggerOperation(Summary = "탑승 기록")]
    [ProducesResponseType(typeof(ResponseModel<PickupPlanDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdatePickupPlanBoard([FromBody] UpdatePickupPlanBoardDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            var model = await _pickupService.UpdatePickupPlanBoardService(dto, cancellationToken).ConfigureAwait(false);
            return ToActionResult(model);
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
            // 앱이 요청을 끊었다. 오류로 기록하지 않는다
            return StatusCode(StatusCodes.Status499ClientClosedRequest);
        }
        catch (Exception ex)
        {
            _logService.LogMessage(ex.ToString());
#if DEBUG
            _logService.ConsoleWarning(ex.ToString());
#endif
            return Problem("서버에서 요청을 처리하지 못하였습니다. 잠시후 다시 시도해주세요.", statusCode: 500);
        }
    }

    /// <summary>
    /// 대기 장소 도착(같은 학원가의 주차장을 기록하고 계획을 WAITING으로 바꾼다)
    /// </summary>
    [Produces("application/json")]
    [HttpPatch]
    [Route("v1/Pickup/M/sign/UpdatePickupPlanWait")]
    [SwaggerOperation(Summary = "대기 장소 도착")]
    [ProducesResponseType(typeof(ResponseModel<PickupPlanDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdatePickupPlanWait([FromBody] UpdatePickupPlanWaitDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            var model = await _pickupService.UpdatePickupPlanWaitService(dto, cancellationToken).ConfigureAwait(false);
            return ToActionResult(model);
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
            // 앱이 요청을 끊었다. 오류로 기록하지 않는다
            return StatusCode(StatusCodes.Status499ClientClosedRequest);
        }
        catch (Exception ex)
        {
            _logService.LogMessage(ex.ToString());
#if DEBUG
            _logService.ConsoleWarning(ex.ToString());
#endif
            return Problem("서버에서 요청을 처리하지 못하였습니다. 잠시후 다시 시도해주세요.", statusCode: 500);
        }
    }

    /// <summary>
    /// 부모 취소(오늘 계획을 CANCELED로 끝내고, 아이가 링크를 받았을 수 있으면 취소 문자를 보낸다)
    /// </summary>
    [Produces("application/json")]
    [HttpDelete]
    [Route("v1/Pickup/M/sign/DeletePickupPlan")]
    [Consumes("application/json")]
    [SwaggerOperation(Summary = "부모 픽업 취소")]
    [ProducesResponseType(typeof(ResponseModel<PickupPlanDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeletePickupPlan([FromBody] DeletePickupPlanDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            var model = await _pickupService.DeletePickupPlanService(dto, cancellationToken).ConfigureAwait(false);
            return ToActionResult(model);
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
            // 앱이 요청을 끊었다. 오류로 기록하지 않는다
            return StatusCode(StatusCodes.Status499ClientClosedRequest);
        }
        catch (Exception ex)
        {
            _logService.LogMessage(ex.ToString());
#if DEBUG
            _logService.ConsoleWarning(ex.ToString());
#endif
            return Problem("서버에서 요청을 처리하지 못하였습니다. 잠시후 다시 시도해주세요.", statusCode: 500);
        }
    }

    /// <summary>
    /// 픽업 장소 추가 요청
    /// </summary>
    /// <remarks>
    /// 후보 지점에 원하는 곳이 없을 때 운영팀에 남기는 요청이다. 계정·아이·시간표를 가리키는 값은 저장하지 않는다.
    /// 응답 code: 200 정상, 400 잘못된 입력(좌표는 둘 다 있거나 둘 다 없어야 한다), 401 인증 실패, 404 내 시간표가 아님, 429 요청 초과(사용자마다 1시간 5건), 500 서버 처리 실패
    /// </remarks>
    [Produces("application/json")]
    [HttpPost]
    [Route("v1/Pickup/M/sign/AddPickupSpotRequest")]
    [Consumes("application/json")]
    [EnableRateLimiting(PickupSpotRequestRateLimit.PolicyName)]
    [SwaggerOperation(Summary = "픽업 장소 추가 요청")]
    [ProducesResponseType(typeof(ResponseModel<AddPickupSpotRequestResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseModel<AddPickupSpotRequestResultDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseModel<AddPickupSpotRequestResultDto>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseModel<AddPickupSpotRequestResultDto>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> AddPickupSpotRequest([FromBody] AddPickupSpotRequestDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            var model = await _pickupService.AddPickupSpotRequestService(dto, cancellationToken).ConfigureAwait(false);
            return ToActionResult(model);
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
            // 앱이 요청을 끊었다. 오류로 기록하지 않는다
            return StatusCode(StatusCodes.Status499ClientClosedRequest);
        }
        catch (Exception ex)
        {
            _logService.LogMessage(ex.ToString());
#if DEBUG
            _logService.ConsoleWarning(ex.ToString());
#endif
            return Problem("서버에서 요청을 처리하지 못하였습니다. 잠시후 다시 시도해주세요.", statusCode: 500);
        }
    }
}
