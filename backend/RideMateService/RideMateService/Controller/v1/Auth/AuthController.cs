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
    /// 토큰 발급
    /// </summary>
    [Produces("application/json")]
    [HttpPost]
    [Route("v1/Auth/M/Token")]
    [Consumes("application/x-www-form-urlencoded")]
    [SwaggerOperation(Summary = "토큰 발급")]
    [ProducesResponseType(typeof(TokenAuthSuccessDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(TokenAuthErrorDto), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Token([FromForm] TokenAuthDto dto, CancellationToken cancellationToken)
    {
        try
        {
            var model = await _authService.TokenAuthService(dto, cancellationToken).ConfigureAwait(false);
            return ToTokenActionResult(model);
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
