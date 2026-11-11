using Microsoft.AspNetCore.Mvc;
using RideMateService.Comm;
using RideMateService.Comm.Dto.v1.Auth.Request;
using RideMateService.Comm.Dto.v1.Auth.Response;
using RideMateService.Comm.Logger;
using RideMateService.Service.v1.Auth;
using Swashbuckle.AspNetCore.Annotations;

namespace RideMateService.Controller.v1.Auth;

[Route("api")]
[ApiController]
public class AuthController : ApiControllerBase
{
    private readonly ILogService _logService;
    private readonly IAuthService _authService;

    public AuthController(ILogService logService, IAuthService authService)
    {
        this._logService = logService;
        this._authService = authService;
    }

    /// <summary>
    /// 인증번호 로그인
    /// </summary>
    [Produces("application/json")]
    [HttpPost]
    [Route("v1/Auth/M/AddLoginSession")]
    [Consumes("application/json")]
    [SwaggerOperation(Summary = "인증번호 로그인")]
    [ProducesResponseType(typeof(ResponseModel<LoginSessionResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseModel<LoginSessionResultDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AddLoginSession([FromBody] AddLoginSessionDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            var model = await _authService.AddLoginSessionService(dto, cancellationToken).ConfigureAwait(false);
            Response.Headers.CacheControl = "no-store";
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
    /// 토큰 갱신
    /// </summary>
    [Produces("application/json")]
    [HttpPatch]
    [Route("v1/Auth/M/UpdateLoginSession")]
    [Consumes("application/json")]
    [SwaggerOperation(Summary = "토큰 갱신")]
    [ProducesResponseType(typeof(ResponseModel<LoginSessionResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseModel<LoginSessionResultDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateLoginSession([FromBody] UpdateLoginSessionDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            var model = await _authService.UpdateLoginSessionService(dto, cancellationToken).ConfigureAwait(false);
            Response.Headers.CacheControl = "no-store";
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
    /// 로그인 인증번호 발송
    /// </summary>
    [Produces("application/json")]
    [HttpPost]
    [Route("v1/Auth/M/AddPhoneVerification")]
    [Consumes("application/json")]
    [SwaggerOperation(Summary = "로그인 인증번호 발송")]
    [ProducesResponseType(typeof(ResponseModel<PhoneVerificationResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> AddPhoneVerification([FromBody] AddPhoneVerificationDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            var model = await _authService.AddPhoneVerificationService(dto, cancellationToken).ConfigureAwait(false);
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
    /// 차량 로그아웃
    /// </summary>
    [Produces("application/json")]
    [HttpDelete]
    [Route("v1/Auth/M/sign/DeleteLoginSession")]
    [SwaggerOperation(Summary = "차량 로그아웃")]
    [ProducesResponseType(typeof(ResponseModel<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteLoginSession(CancellationToken cancellationToken)
    {
        try
        {
            var model = await _authService.DeleteLoginSessionService(cancellationToken).ConfigureAwait(false);
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
