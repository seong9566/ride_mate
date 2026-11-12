using Microsoft.AspNetCore.Mvc;
using RideMateService.Comm;
using RideMateService.Comm.Dto.v1.Schedule.Request;
using RideMateService.Comm.Dto.v1.Schedule.Response;
using RideMateService.Comm.Logger;
using RideMateService.Service.v1.Schedule;
using Swashbuckle.AspNetCore.Annotations;

namespace RideMateService.Controller.v1.Schedule;

[Route("api")]
[ApiController]
public class ScheduleController : ApiControllerBase
{
    private readonly ILogService _logService;
    private readonly IScheduleService _scheduleService;

    public ScheduleController(ILogService logService, IScheduleService scheduleService)
    {
        this._logService = logService;
        this._scheduleService = scheduleService;
    }

    /// <summary>
    /// 시간표 등록
    /// </summary>
    [Produces("application/json")]
    [HttpPost]
    [Consumes("application/json")]
    [Route("v1/Schedule/M/sign/AddSchedule")]
    [SwaggerOperation(Summary = "시간표 등록")]
    [ProducesResponseType(typeof(ResponseModel<AddScheduleResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> AddSchedule([FromBody] AddScheduleDto dto, CancellationToken cancellationToken)
    {
        try
        {
            var model = await _scheduleService.AddScheduleService(dto, cancellationToken).ConfigureAwait(false);
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
    /// 내 시간표 목록
    /// </summary>
    [Produces("application/json")]
    [HttpGet]
    [Route("v1/Schedule/M/sign/GetScheduleList")]
    [SwaggerOperation(Summary = "내 시간표 목록")]
    [ProducesResponseType(typeof(ResponseModel<List<ScheduleItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetScheduleList(CancellationToken cancellationToken)
    {
        try
        {
            var model = await _scheduleService.GetScheduleListService(cancellationToken).ConfigureAwait(false);
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
    /// 요일별 픽업 스위치 변경
    /// </summary>
    [Produces("application/json")]
    [HttpPatch]
    [Consumes("application/json")]
    [Route("v1/Schedule/M/sign/UpdateScheduleDayPickup")]
    [SwaggerOperation(Summary = "요일별 픽업 스위치 변경")]
    [ProducesResponseType(typeof(ResponseModel<ScheduleDayDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateScheduleDayPickup([FromBody] UpdateScheduleDayPickupDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            var model = await _scheduleService.UpdateScheduleDayPickupService(dto, cancellationToken).ConfigureAwait(false);
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
    /// 우리 픽업 장소 저장
    /// </summary>
    /// <remarks>
    /// 응답 code: 200 정상, 400 잘못된 입력, 401 인증 실패, 404 시간표 없음, 409 후보가 아닌 지점(INVALID_PICKUP_SPOT), 500 서버 처리 실패
    /// </remarks>
    [Produces("application/json")]
    [HttpPatch]
    [Consumes("application/json")]
    [Route("v1/Schedule/M/sign/UpdateSchedulePickupSpot")]
    [SwaggerOperation(Summary = "우리 픽업 장소 저장")]
    [ProducesResponseType(typeof(ResponseModel<UpdateSchedulePickupSpotResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseModel<UpdateSchedulePickupSpotResultDto>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateSchedulePickupSpot([FromBody] UpdateSchedulePickupSpotDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            var model = await _scheduleService.UpdateSchedulePickupSpotService(dto, cancellationToken).ConfigureAwait(false);
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
    /// 시간표 편집
    /// </summary>
    [Produces("application/json")]
    [HttpPatch]
    [Consumes("application/json")]
    [Route("v1/Schedule/M/sign/UpdateSchedule")]
    [SwaggerOperation(Summary = "시간표 편집")]
    [ProducesResponseType(typeof(ResponseModel<AddScheduleResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateSchedule([FromBody] UpdateScheduleDto dto, CancellationToken cancellationToken)
    {
        try
        {
            var model = await _scheduleService.UpdateScheduleService(dto, cancellationToken).ConfigureAwait(false);
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
    /// 시간표 삭제
    /// </summary>
    [Produces("application/json")]
    [HttpDelete]
    [Consumes("application/json")]
    [Route("v1/Schedule/M/sign/DeleteSchedule")]
    [SwaggerOperation(Summary = "시간표 삭제")]
    [ProducesResponseType(typeof(ResponseModel<DeleteScheduleResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteSchedule([FromBody] DeleteScheduleDto dto, CancellationToken cancellationToken)
    {
        try
        {
            var model = await _scheduleService.DeleteScheduleService(dto, cancellationToken).ConfigureAwait(false);
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
