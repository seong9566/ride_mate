using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using RideMateService.Comm;
using RideMateService.Comm.Dto.v1.Auth.Request;
using RideMateService.Comm.Dto.v1.Auth.Response;
using RideMateService.Comm.Logger;
using RideMateService.Comm.Security;
using RideMateService.Service.v1.Auth;
using Swashbuckle.AspNetCore.Annotations;

namespace RideMateService.Controller.v1.Auth;

[Route("api")]
[ApiController]
public class AuthWebController : ApiControllerBase
{
    private const int WebSessionDays = 90;

    private readonly ILogService _logService;
    private readonly IAuthService _authService;

    public AuthWebController(ILogService logService, IAuthService authService)
    {
        this._logService = logService;
        this._authService = authService;
    }

    /// <summary>
    /// 휴대폰 웹 QR 로그인 진입
    /// </summary>
    [Produces("application/json")]
    [HttpGet]
    [Route("v1/Auth/W/DetailQrLogin")]
    [SwaggerOperation(Summary = "QR 로그인 진입(휴대폰 웹)")]
    [ProducesResponseType(typeof(ResponseModel<WebQrLoginDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DetailQrLogin(
        [FromQuery, Required, StringLength(64, MinimumLength = 1)] string userCode,
        CancellationToken cancellationToken)
    {
        try
        {
            var authentication = await HttpContext.AuthenticateAsync(WebSessionAuthenticationHandler.AuthenticationScheme)
                .ConfigureAwait(false);
            int? userId = null;
            if (authentication.Succeeded
                && int.TryParse(authentication.Principal?.FindFirstValue("sub"), out var authenticatedUserId))
                userId = authenticatedUserId;

            var model = await _authService.DetailQrLoginService(userCode, userId, cancellationToken).ConfigureAwait(false);
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
    /// 휴대폰 웹 로그인 인증번호 발송
    /// </summary>
    [Produces("application/json")]
    [HttpPost]
    [Route("v1/Auth/W/AddPhoneVerification")]
    [Consumes("application/json")]
    [SwaggerOperation(Summary = "로그인 인증번호 발송(휴대폰 웹)")]
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
    /// 휴대폰 웹 인증번호 확인·가입
    /// </summary>
    [Produces("application/json")]
    [HttpPost]
    [Route("v1/Auth/W/AddWebSession")]
    [Consumes("application/json")]
    [SwaggerOperation(Summary = "인증번호 확인과 가입(휴대폰 웹)")]
    [ProducesResponseType(typeof(ResponseModel<WebQrLoginDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> AddWebSession([FromBody] AddWebSessionDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            var model = await _authService.AddWebSessionService(dto, cancellationToken).ConfigureAwait(false);
            var webSessionToken = model.data?.webSessionToken;
            if (model.code == 200 && !string.IsNullOrEmpty(webSessionToken))
                SetWebSessionCookie(webSessionToken);
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
    /// 휴대폰 웹 QR 로그인 허용
    /// </summary>
    [Produces("application/json")]
    [HttpPost]
    [Route("v1/Auth/W/sign/AddQrApproval")]
    [Consumes("application/json")]
    [SwaggerOperation(Summary = "QR 로그인 허용(휴대폰 웹)")]
    [ProducesResponseType(typeof(ResponseModel<WebQrLoginDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> AddQrApproval([FromBody] AddQrApprovalDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            var model = await _authService.AddQrApprovalService(dto, cancellationToken).ConfigureAwait(false);
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
    /// 휴대폰 웹 QR 로그인 취소
    /// </summary>
    [Produces("application/json")]
    [HttpDelete]
    [Route("v1/Auth/W/sign/DeleteQrLogin")]
    [Consumes("application/json")]
    [SwaggerOperation(Summary = "QR 로그인 취소(휴대폰 웹)")]
    [ProducesResponseType(typeof(ResponseModel<WebQrLoginDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteQrLogin([FromBody] DeleteWebQrLoginDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            var model = await _authService.DeleteWebQrLoginService(dto, cancellationToken).ConfigureAwait(false);
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
    /// 휴대폰 웹 세션 종료
    /// </summary>
    [Produces("application/json")]
    [HttpDelete]
    [Route("v1/Auth/W/sign/DeleteWebSession")]
    [Consumes("application/json")]
    [SwaggerOperation(Summary = "다른 번호로 로그인(휴대폰 웹)")]
    [ProducesResponseType(typeof(ResponseModel<WebQrLoginDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteWebSession([FromBody] DeleteWebSessionDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            var model = await _authService.DeleteWebSessionService(dto, cancellationToken).ConfigureAwait(false);
            if (model.code == 200)
                DeleteWebSessionCookie();
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

    private void SetWebSessionCookie(string token)
    {
        Response.Cookies.Append(WebSessionAuthenticationHandler.CookieName, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            MaxAge = TimeSpan.FromDays(WebSessionDays)
        });
    }

    private void DeleteWebSessionCookie()
    {
        Response.Cookies.Delete(WebSessionAuthenticationHandler.CookieName, new CookieOptions { Path = "/" });
    }
}
