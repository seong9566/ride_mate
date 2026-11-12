using Microsoft.AspNetCore.Mvc;
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

    public PickupController(ILogService logService, IPickupService pickupService)
    {
        this._logService = logService;
        this._pickupService = pickupService;
    }

    /// <summary>
    /// 출발 안내 계산
    /// </summary>
    [Produces("application/json")]
    [HttpGet]
    [Route("v1/Pickup/M/sign/DetailDepartureGuide")]
    [SwaggerOperation(Summary = "출발 안내 계산")]
    [ProducesResponseType(typeof(ResponseModel<DetailDepartureGuideDto>), StatusCodes.Status200OK)]
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
    /// 대기 장소 추천 목록
    /// </summary>
    /// <remarks>
    /// 응답 code: 101 오늘 남은 픽업 없음, 200 정상, 400 잘못된 입력, 401 인증 실패, 500 서버 처리 실패
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
    /// 오늘 픽업 계획 만들기
    /// </summary>
    [Produces("application/json")]
    [HttpPost]
    [Route("v1/Pickup/M/sign/AddPickupPlan")]
    [SwaggerOperation(Summary = "오늘 픽업 계획 만들기")]
    [ProducesResponseType(typeof(ResponseModel<PickupPlanDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> AddPickupPlan([FromBody] AddPickupPlanDto dto, CancellationToken cancellationToken)
    {
        try
        {
            var model = await _pickupService.AddPickupPlanService(dto, cancellationToken).ConfigureAwait(false);
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
    /// 경로 시작(늦게 출발했으면 같은 장소에서 배정 시각을 미룬다)
    /// </summary>
    [Produces("application/json")]
    [HttpPatch]
    [Route("v1/Pickup/M/sign/UpdatePickupPlanStart")]
    [SwaggerOperation(Summary = "경로 시작")]
    [ProducesResponseType(typeof(ResponseModel<PickupPlanDto>), StatusCodes.Status200OK)]
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
}
