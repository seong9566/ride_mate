using Microsoft.AspNetCore.Mvc;
using RideMateService.Comm;

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
}
