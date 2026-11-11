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
}
