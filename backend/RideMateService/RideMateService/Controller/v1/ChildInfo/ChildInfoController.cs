using Microsoft.AspNetCore.Mvc;
using RideMateService.Comm;
using RideMateService.Comm.Dto.v1.ChildInfo.Request;
using RideMateService.Comm.Dto.v1.ChildInfo.Response;
using RideMateService.Comm.Logger;
using RideMateService.Service.v1.ChildInfo;
using Swashbuckle.AspNetCore.Annotations;

namespace RideMateService.Controller.v1.ChildInfo;

[Route("api")]
[ApiController]
public class ChildInfoController : ApiControllerBase
{
    private readonly ILogService _logService;
    private readonly IChildInfoService _childInfoService;

    public ChildInfoController(ILogService logService, IChildInfoService childInfoService)
    {
        this._logService = logService;
        this._childInfoService = childInfoService;
    }

    /// <summary>
    /// 아이 정보 저장
    /// </summary>
    [Produces("application/json")]
    [HttpPost]
    [Consumes("application/json")]
    [Route("v1/ChildInfo/M/sign/AddChild")]
    [SwaggerOperation(Summary = "아이 정보 저장")]
    [ProducesResponseType(typeof(ResponseModel<AddChildResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseModel<AddChildResultDto>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddChild([FromBody] AddChildDto dto, CancellationToken cancellationToken)
    {
        try
        {
            var model = await _childInfoService.AddChildService(dto, cancellationToken).ConfigureAwait(false);
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
