using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Authorization;

namespace RideMateService.Comm;

/// <summary>
/// 라우트 템플릿에 /sign/ 구간이 있는 액션에 기본 인증 정책(AuthorizeFilter)을 붙인다.
/// 액션이나 컨트롤러에 [Authorize]·[AllowAnonymous]가 직접 붙어 있으면 건드리지 않는다.
/// </summary>
public class SignRouteConvention : IActionModelConvention
{
    /// <summary>
    /// 액션 모델을 검사해 sign 구간이면 AuthorizeFilter를 더한다.
    /// </summary>
    public void Apply(ActionModel action)
    {
        var hasAuthAttribute = action.Attributes.Concat(action.Controller.Attributes)
            .Any(a => a is IAuthorizeData || a is IAllowAnonymous);
        if (hasAuthAttribute)
            return;

        var isSignRoute = action.Selectors
            .Select(s => s.AttributeRouteModel?.Template)
            .Any(t => t != null && ("/" + t.Trim('/') + "/").Contains("/sign/", StringComparison.OrdinalIgnoreCase));
        if (!isSignRoute)
            return;

        action.Filters.Add(new AuthorizeFilter());
    }
}
