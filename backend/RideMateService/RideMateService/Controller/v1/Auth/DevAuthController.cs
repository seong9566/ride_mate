using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RideMateService.Comm;
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

    public DevAuthController(ILogService logService, IAuthService authService)
    {
        this._logService = logService;
        this._authService = authService;
    }

    /// <summary>
    /// 개발 전용 토큰 발급
    /// </summary>
    /// <remarks>
    /// 개발 단계라 배포 서버(Production)에서도 동작한다. 출시 전에 막는다. 클라이언트(IP)마다 1분 30회다(넘으면 429). 배포 서버는 프록시 뒤라 지금은 모든 호출이 한 묶음으로 센다.
    /// 인증번호·QR 없이 계정 ID로 로그인과 같은 차량 세션을 만들고 `Auth/M/Token`과 같은 본문을 돌려준다.
    /// 받은 access_token을 Swagger의 Authorize에 넣는다. device_id를 비우면 swagger-dev이고, 값을 넣으면 같은 기기 ID로 로그인해 있던 그 계정의 실제 차량 세션이 끝난다.
    /// 없는 계정이면 400 `invalid_grant`다.
    /// </remarks>
    [Produces("application/json")]
    [HttpPost]
    [Consumes("application/json")]
    [Route("v1/Auth/M/DevToken")]
    [EnableRateLimiting(DevTokenRateLimit.PolicyName)]
    [SwaggerOperation(Summary = "개발 전용 토큰 발급")]
    [ProducesResponseType(typeof(TokenAuthSuccessDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(TokenAuthErrorDto), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DevToken([FromBody] AddDevTokenDto dto, CancellationToken cancellationToken)
    {
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
