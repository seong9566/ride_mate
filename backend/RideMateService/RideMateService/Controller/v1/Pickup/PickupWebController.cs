using Microsoft.AspNetCore.Cors;
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
[EnableCors(ChildWebCors.PolicyName)]
public class PickupWebController : ApiControllerBase
{
    private readonly JsonSerializerSettings _jsonSettings;
    private readonly ILogService _logService;
    private readonly IPickupService _pickupService;

    public PickupWebController(ILogService logService, IPickupService pickupService,
        IOptions<MvcNewtonsoftJsonOptions> jsonOptions)
    {
        this._jsonSettings = jsonOptions.Value.SerializerSettings;
        this._logService = logService;
        this._pickupService = pickupService;
    }

    /// <summary>
    /// 아이 픽업 안내 조회
    /// </summary>
    /// <remarks>
    /// X-Pickup-Token 헤더로 픽업 링크 토큰을 전달한다. 끝난 링크도 200이고 linkStatus(ACTIVE·BOARDED·CLOSED)로 나눈다.
    /// 응답 code: 200 정상, 404 링크 없음, 429 요청 초과, 500 서버 처리 실패
    /// </remarks>
    [Produces("application/json")]
    [HttpGet]
    [Route("v1/Pickup/W/DetailChildPickup")]
    [ChildPickupLimit(ChildPickupRateLimit.ViewGroup, ChildPickupRateLimit.ViewPermitLimit)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [SwaggerOperation(Summary = "아이 픽업 안내 조회(휴대폰 웹)")]
    [ProducesResponseType(typeof(ResponseModel<ChildPickupDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseModel<ChildPickupDto>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DetailChildPickup(
        [FromHeader(Name = "X-Pickup-Token")] string? token,
        CancellationToken cancellationToken)
    {
        try
        {
            var model = await _pickupService.DetailChildPickupService(token, cancellationToken)
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
    /// 아이 픽업 링크 열람 기록
    /// </summary>
    /// <remarks>
    /// X-Pickup-Token 헤더로 픽업 링크 토큰을 전달한다. 본문 없음. 웹이 화면을 처음 그린 뒤 한 번 부른다. 열람은 한 번만 기록하고, 다시 부르면 첫 열람 시각을 돌려준다.
    /// 응답 code: 200 정상, 404 링크 없음, 409 링크 종료, 429 요청 초과, 500 서버 처리 실패
    /// </remarks>
    [Produces("application/json")]
    [HttpPost]
    [Route("v1/Pickup/W/AddPickupLinkOpen")]
    [ChildPickupLimit(ChildPickupRateLimit.OpenGroup, ChildPickupRateLimit.OpenPermitLimit)]
    [SwaggerOperation(Summary = "아이 픽업 링크 열람 기록(휴대폰 웹)")]
    [ProducesResponseType(typeof(ResponseModel<ChildPickupOpenDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseModel<ChildPickupOpenDto>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseModel<ChildPickupOpenDto>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(void), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> AddPickupLinkOpen(
        [FromHeader(Name = "X-Pickup-Token")] string? token,
        CancellationToken cancellationToken)
    {
        try
        {
            var model = await _pickupService.AddPickupLinkOpenService(token, cancellationToken)
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

    /// <summary>아이 픽업 안내 실시간 연결(SSE)</summary>
    /// <remarks>X-Pickup-Token 헤더로 픽업 링크 토큰을 전달한다(EventSource는 헤더를 못 붙이므로 웹은 fetch 스트리밍으로 읽는다).
    /// 판정 실패는 SSE가 아니라 JSON이다: 404 링크 없음, 429 요청 초과, 500.
    /// 성공하면 text/event-stream으로 연결 직후 `snapshot`(화면 전체) 하나, 이후 바뀔 때마다 `snapshot`(change 포함)과 `vehicle`(차 위치)을 보낸다. 15초마다 `: ping` 주석 줄. 링크가 끝나거나(EXPIRE_DT 시각 포함) 30분이 지나거나 같은 계획에 연결이 5개를 넘어 가장 오래된 연결이면 서버가 닫는다. 다시 연결하면 snapshot부터 받는다(놓친 이벤트 재생 없음).</remarks>
    [HttpGet]
    [Route("v1/Pickup/W/DetailChildPickupStream")]
    [ChildPickupLimit(ChildPickupRateLimit.StreamGroup, ChildPickupRateLimit.StreamPermitLimit)]
    [SwaggerOperation(Summary = "아이 픽업 안내 실시간 연결(휴대폰 웹, SSE)")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseModel<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DetailChildPickupStream(
        [FromHeader(Name = "X-Pickup-Token")] string? token, CancellationToken cancellationToken)
    {
        int? pickupPlanId = null;
        try
        {
            var model = await _pickupService.OpenChildPickupStreamService(token, cancellationToken)
                .ConfigureAwait(false);
            if (model.code != 200)
                return ToActionResult(model);

            pickupPlanId = model.data!.PickupPlanId;
            await SseWriter.WriteAsync(Response, _pickupService.ReadChildPickupStreamAsync(model.data!, cancellationToken),
                _jsonSettings, cancellationToken).ConfigureAwait(false);
            return new EmptyResult();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new EmptyResult();
        }
        catch (Exception ex)
        {
            var message = $"child pickup stream failed: plan={pickupPlanId?.ToString() ?? "unknown"}\n{ex}";
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
