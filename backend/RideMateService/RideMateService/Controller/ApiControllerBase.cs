using Microsoft.AspNetCore.Mvc;
using RideMateService.Comm;
using RideMateService.Comm.Dto.v1.Auth.Response;

namespace RideMateService.Controller;

public abstract class ApiControllerBase : ControllerBase
{
    private const string InternalErrorMessage = "서버에서 요청을 처리하지 못하였습니다. 잠시후 다시 시도해주세요.";

    protected IActionResult ToActionResult<T>(ResponseModel<T>? model, bool includeNotFound = false)
    {
        if (model is null)
        {
            return Problem(InternalErrorMessage, statusCode: 500);
        }

        return model.code switch
        {
            101 => Ok(model),
            102 => Ok(model),
            103 => Ok(model),
            201 => Ok(model),
            202 => Ok(model),
            200 => Ok(model),
            400 => BadRequest(model),
            401 => Unauthorized(model),
            403 => StatusCode(StatusCodes.Status403Forbidden, model),
            404 => NotFound(model),
            409 => Conflict(model),
            _   => Problem(InternalErrorMessage, statusCode: 500)
        };
    }

    /// <summary>
    /// 토큰 발급 결과를 OAuth 2.0 본문으로 바꾼다. 200은 success 본문과 Cache-Control: no-store, 400은 error 본문, 그 밖은 500(ProblemDetails)이다.
    /// </summary>
    protected IActionResult ToTokenActionResult(ResponseModel<TokenAuthResultDto>? model)
    {
        if (model?.code == 200 && model.data?.success is not null)
        {
            Response.Headers.CacheControl = "no-store";
            return Ok(model.data.success);
        }

        if (model?.code == 400 && model.data?.error is not null)
        {
            return BadRequest(model.data.error);
        }

        return Problem(InternalErrorMessage, statusCode: 500);
    }
}
