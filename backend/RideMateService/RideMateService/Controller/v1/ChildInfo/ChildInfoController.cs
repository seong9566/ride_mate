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

    /// <summary>
    /// 아이 정보 수정
    /// </summary>
    /// <remarks>
    /// childName·phone·sendChannel은 보낸 것만 바꾸고 빠지거나 null이면 그대로 둔다. 셋 다 없으면 400이다.
    /// 지금 번호와 같은 번호를 보내면 바꾸지 않은 것으로 보고, 바뀐 것이 없으면 쓰지 않고 저장된 값을 200으로 돌려준다.
    ///
    /// 응답 code
    /// - 200: 수정 완료. data에 저장된 값
    /// - 101: 아이 정보 동의가 없음
    /// - 102: 아이 번호가 부모 번호와 같음
    /// - 103: 다른 계정에 이미 등록된 번호
    /// - 400: 잘못된 요청 (형식 오류, 바꿀 값 없음)
    /// - 401: 인증 실패
    /// - 404: 이 계정의 아이가 아님
    /// - 500: 서버 오류
    /// </remarks>
    [Produces("application/json")]
    [HttpPatch]
    [Consumes("application/json")]
    [Route("v1/ChildInfo/M/sign/UpdateChild")]
    [SwaggerOperation(Summary = "아이 정보 수정")]
    [ProducesResponseType(typeof(ResponseModel<AddChildResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseModel<AddChildResultDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateChild([FromBody] UpdateChildDto dto, CancellationToken cancellationToken)
    {
        try
        {
            var model = await _childInfoService.UpdateChildService(dto, cancellationToken).ConfigureAwait(false);
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
