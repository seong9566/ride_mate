using Microsoft.AspNetCore.Mvc;
using RideMateService.Comm;
using RideMateService.Comm.Dto.v1.User.Request;
using RideMateService.Comm.Dto.v1.User.Response;
using RideMateService.Comm.Logger;
using RideMateService.Service.v1.User;
using Swashbuckle.AspNetCore.Annotations;

namespace RideMateService.Controller.v1.User;

[Route("api")]
[ApiController]
public class UserController : ApiControllerBase
{
    private readonly ILogService _logService;
    private readonly IUserService _userService;

    public UserController(ILogService logService, IUserService userService)
    {
        this._logService = logService;
        this._userService = userService;
    }

    /// <summary>
    /// 집 저장
    /// </summary>
    [Produces("application/json")]
    [HttpPatch]
    [Consumes("application/json")]
    [Route("v1/User/M/sign/UpdateHome")]
    [SwaggerOperation(Summary = "집 저장")]
    [ProducesResponseType(typeof(ResponseModel<HomeDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateHome([FromBody] UpdateHomeDto dto, CancellationToken cancellationToken)
    {
        try
        {
            var model = await _userService.UpdateHomeService(dto, cancellationToken).ConfigureAwait(false);
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
    /// 차 정보 저장
    /// </summary>
    [Produces("application/json")]
    [HttpPatch]
    [Consumes("application/json")]
    [Route("v1/User/M/sign/UpdateVehicle")]
    [SwaggerOperation(Summary = "차 정보 저장")]
    [ProducesResponseType(typeof(ResponseModel<VehicleDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateVehicle([FromBody] UpdateVehicleDto dto, CancellationToken cancellationToken)
    {
        try
        {
            var model = await _userService.UpdateVehicleService(dto, cancellationToken).ConfigureAwait(false);
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
    /// 초기 설정 완료
    /// </summary>
    [Produces("application/json")]
    [HttpPatch]
    [Route("v1/User/M/sign/UpdateSetupComplete")]
    [SwaggerOperation(Summary = "초기 설정 완료")]
    [ProducesResponseType(typeof(ResponseModel<SetupCompleteDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateSetupComplete(CancellationToken cancellationToken)
    {
        try
        {
            var model = await _userService.UpdateSetupCompleteService(cancellationToken).ConfigureAwait(false);
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
    /// 계정 정보 조회
    /// </summary>
    /// <remarks>
    /// 응답 code: 200 정상, 401 인증 실패, 500 서버 처리 실패
    /// </remarks>
    [Produces("application/json")]
    [HttpGet]
    [Route("v1/User/M/sign/DetailUser")]
    [SwaggerOperation(Summary = "계정 정보 조회")]
    [ProducesResponseType(typeof(ResponseModel<DetailUserDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DetailUser(CancellationToken cancellationToken)
    {
        try
        {
            var model = await _userService.DetailUserService(cancellationToken).ConfigureAwait(false);
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
