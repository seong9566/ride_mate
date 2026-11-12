using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RideMateService.Comm;
using RideMateService.Comm.Dto.v1.Pickup.Request;
using RideMateService.Comm.Dto.v1.Pickup.Response;
using RideMateService.Comm.Logger;
using RideMateService.Service.v1.Pickup;
using Swashbuckle.AspNetCore.Annotations;

namespace RideMateService.Controller.v1.Pickup;

[Route("api")]
[ApiController]
public class PickupWebController : ApiControllerBase
{
    private readonly ILogService _logService;
    private readonly IPickupService _pickupService;

    public PickupWebController(ILogService logService, IPickupService pickupService)
    {
        this._logService = logService;
        this._pickupService = pickupService;
    }

    /// <summary>
    /// 아이 출발 알림 등록
    /// </summary>
    /// <remarks>
    /// X-Pickup-Token 헤더로 픽업 링크 토큰을 전달한다.
    /// 응답 code: 200 정상, 400 leaveNow·leaveDt 조합 오류·출발 시각 범위 밖(본문 형식 오류는 ProblemDetails), 404 링크 없음, 409 링크 종료 또는 이미 출발, 429 요청 초과, 500 서버 처리 실패
    /// </remarks>
    [Produces("application/json")]
    [HttpPost]
    [Route("v1/Pickup/W/AddChildLeave")]
    [Consumes("application/json")]
    [EnableRateLimiting(ChildLeaveRateLimit.PolicyName)]
    [SwaggerOperation(Summary = "아이 출발 알림 등록(휴대폰 웹)")]
    [ProducesResponseType(typeof(ResponseModel<ChildLeaveDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseModel<ChildLeaveDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseModel<ChildLeaveDto>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseModel<ChildLeaveDto>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(void), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> AddChildLeave(
        [FromHeader(Name = "X-Pickup-Token")] string? token,
        [FromBody] AddChildLeaveDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            var model = await _pickupService.AddChildLeaveService(token, dto, cancellationToken)
                .ConfigureAwait(false);
            return ToActionResult(model);
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
            // 웹 요청이 끊겼다. 오류로 기록하지 않는다
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
    /// 아이 출발 예정 시각 취소
    /// </summary>
    /// <remarks>
    /// X-Pickup-Token 헤더로 픽업 링크 토큰을 전달한다.
    /// 응답 code: 200 정상, 404 링크 없음, 409 링크 종료 또는 이미 출발, 429 요청 초과, 500 서버 처리 실패
    /// </remarks>
    [Produces("application/json")]
    [HttpDelete]
    [Route("v1/Pickup/W/DeleteChildLeave")]
    [EnableRateLimiting(ChildLeaveRateLimit.PolicyName)]
    [SwaggerOperation(Summary = "아이 출발 예정 시각 취소(휴대폰 웹)")]
    [ProducesResponseType(typeof(ResponseModel<ChildLeaveDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseModel<ChildLeaveDto>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseModel<ChildLeaveDto>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(void), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteChildLeave(
        [FromHeader(Name = "X-Pickup-Token")] string? token,
        CancellationToken cancellationToken)
    {
        try
        {
            var model = await _pickupService.DeleteChildLeaveService(token, cancellationToken).ConfigureAwait(false);
            return ToActionResult(model);
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
            // 웹 요청이 끊겼다. 오류로 기록하지 않는다
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
