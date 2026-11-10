using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;

namespace RideMateService.Comm;

/// <summary>
/// 최종 경로에 /sign/ 구간이 있는 엔드포인트에 기본 인증 정책을 붙이는 규약.
/// 액션이나 컨트롤러에 [Authorize] 계열이 직접 붙어 있으면 건드리지 않고,
/// [AllowAnonymous]가 붙어 있으면 기동을 실패시킨다.
/// </summary>
public static class SignRouteConvention
{
    /// <summary>
    /// 컨트롤러 엔드포인트의 최종 경로를 보고 sign 구간이면 [Authorize]를 더한다.
    /// </summary>
    public static ControllerActionEndpointConventionBuilder RequireAuthForSignRoutes(
        this ControllerActionEndpointConventionBuilder builder)
    {
        builder.Add(endpointBuilder =>
        {
            if (endpointBuilder is not RouteEndpointBuilder routeBuilder)
                return;

            var route = routeBuilder.RoutePattern.RawText ?? string.Empty;
            if (!IsSignRoute(route))
                return;

            if (routeBuilder.Metadata.OfType<IAllowAnonymous>().Any())
                throw new InvalidOperationException($"sign 구간 라우트에는 [AllowAnonymous]를 쓸 수 없습니다: {route}");

            if (routeBuilder.Metadata.OfType<IAuthorizeData>().Any())
                return;

            routeBuilder.Metadata.Add(new AuthorizeAttribute());
        });

        return builder;
    }

    /// <summary>
    /// 경로에 sign 구간(/sign/, 대소문자 무시)이 있는지 본다. signature 같은 구간은 해당하지 않는다.
    /// </summary>
    public static bool IsSignRoute(string? route)
    {
        return ("/" + (route ?? string.Empty).Trim('/') + "/").Contains("/sign/", StringComparison.OrdinalIgnoreCase);
    }
}
