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
}
