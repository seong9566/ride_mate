using Microsoft.AspNetCore.Mvc;
using RideMateService.Comm;
using RideMateService.Comm.Dto.v1.{도메인}.Request;
using RideMateService.Comm.Dto.v1.{도메인}.Response;
using RideMateService.Comm.Logger;
using RideMateService.Service.v1.{도메인};
using Swashbuckle.AspNetCore.Annotations;

namespace RideMateService.Controller.v1.{도메인};

[Route("api")]
[ApiController]
public class {도메인}Controller : ApiControllerBase
{
    private readonly ILogService _logService;
    private readonly I{도메인}Service _{도메인소문자}Service;

    public {도메인}Controller(ILogService logService, I{도메인}Service {도메인소문자}Service)
    {
        this._logService = logService;
        this._{도메인소문자}Service = {도메인소문자}Service;
    }

    /// <summary>
    /// {한글 설명}
    /// </summary>
    [Produces("application/json")]
    [HttpPost]
    [Route("v1/{도메인}/M/sign/Add{도메인}")]
    [SwaggerOperation(Summary = "{한글 설명}")]
    [ProducesResponseType(typeof(ResponseModel<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Add{도메인}([FromBody] Add{도메인}Dto dto, CancellationToken cancellationToken)
    {
        try
        {
            var model = await _{도메인소문자}Service.Add{도메인}Service(dto, cancellationToken).ConfigureAwait(false);
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
    /// {한글 설명}
    /// </summary>
    [Produces("application/json")]
    [HttpGet]
    [Route("v1/{도메인}/M/sign/Get{도메인}List")]
    [SwaggerOperation(Summary = "{한글 설명}")]
    [ProducesResponseType(typeof(ResponseModel<CursorResult<Get{도메인}List>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get{도메인}List([FromQuery] int? last{도메인}Seq, [FromQuery] int pageSize, [FromQuery] string? searchKey, CancellationToken cancellationToken)
    {
        try
        {
            var model = await _{도메인소문자}Service.Get{도메인}ListService(last{도메인}Seq, pageSize, searchKey, cancellationToken).ConfigureAwait(false);
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
