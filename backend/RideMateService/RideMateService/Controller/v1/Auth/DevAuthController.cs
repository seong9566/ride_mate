using Microsoft.AspNetCore.Mvc;
using RideMateService.Comm.Dto.v1.Auth.Request;
using RideMateService.Comm.Dto.v1.Auth.Response;
using RideMateService.Comm.Logger;
using RideMateService.Service.v1.Auth;
using Swashbuckle.AspNetCore.Annotations;

namespace RideMateService.Controller.v1.Auth;

[Route("api")]
[ApiController]
public class DevAuthController : ApiControllerBase
{
    private readonly ILogService _logService;
    private readonly IAuthService _authService;
    private readonly IWebHostEnvironment _environment;

    public DevAuthController(ILogService logService, IAuthService authService, IWebHostEnvironment environment)
    {
        this._logService = logService;
        this._authService = authService;
        this._environment = environment;
    }

    /// <summary>
    /// 개발 전용 토큰 발급
    /// </summary>
    /// <remarks>
    /// Development 환경에서만 동작한다. 그 밖의 환경(배포 서버)은 404다.
    /// 인증번호·QR 없이 계정 ID로 로그인과 같은 차량 세션을 만들고 `Auth/M/Token`과 같은 본문을 돌려준다.
    /// 받은 access_token을 Swagger의 Authorize에 넣는다. device_id를 비우면 swagger-dev이고, 같은 기기 ID의 그 계정 차량 세션은 끝난다.
    /// 없는 계정이면 400 `invalid_grant`다.
    /// </remarks>
    [Produces("application/json")]
    [HttpPost]
    [Consumes("application/json")]
    [Route("v1/Auth/M/DevToken")]
    [SwaggerOperation(Summary = "개발 전용 토큰 발급")]
    [ProducesResponseType(typeof(TokenAuthSuccessDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(TokenAuthErrorDto), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DevToken([FromBody] AddDevTokenDto dto, CancellationToken cancellationToken)
    {
        if (!_environment.IsDevelopment())
            return NotFound();

        try
        {
            var model = await _authService.AddDevTokenService(dto, cancellationToken).ConfigureAwait(false);
            return ToTokenActionResult(model);
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
            // 요청이 끊겼다. 오류로 기록하지 않는다
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
