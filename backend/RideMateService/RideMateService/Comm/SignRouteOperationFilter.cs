using Microsoft.OpenApi;
using RideMateService.Comm.Security;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace RideMateService.Comm;

/// <summary>
/// 경로에 /sign/ 구간이 있는 Swagger 작업에 클라이언트별 보안 요구를 붙인다.
/// </summary>
public class SignRouteOperationFilter : IOperationFilter
{
    /// <summary>
    /// M/sign에는 Bearer, W/sign에는 WebSession 보안 요구를 더한다.
    /// </summary>
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (!SignRouteConvention.IsSignRoute(context.ApiDescription.RelativePath))
            return;

        operation.Security ??= new List<OpenApiSecurityRequirement>();
        var securityScheme = SignRouteConvention.IsWebSignRoute(context.ApiDescription.RelativePath)
            ? WebSessionAuthenticationHandler.AuthenticationScheme
            : "Bearer";
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(securityScheme, context.Document, null)] = new List<string>()
        });
    }
}
