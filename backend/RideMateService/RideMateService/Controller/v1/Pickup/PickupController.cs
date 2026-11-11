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
}
