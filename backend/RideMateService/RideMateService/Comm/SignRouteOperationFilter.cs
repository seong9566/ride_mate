using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace RideMateService.Comm;

/// <summary>
/// 경로에 /sign/ 구간이 있는 Swagger 작업에만 Bearer 보안 요구를 붙인다.
/// </summary>
public class SignRouteOperationFilter : IOperationFilter
{
    /// <summary>
    /// sign 구간 작업에 Bearer 보안 요구를 더한다.
    /// </summary>
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var path = "/" + (context.ApiDescription.RelativePath ?? string.Empty).Trim('/') + "/";
        if (!path.Contains("/sign/", StringComparison.OrdinalIgnoreCase))
            return;

        operation.Security ??= new List<OpenApiSecurityRequirement>();
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", context.Document, null)] = new List<string>()
        });
    }
}
