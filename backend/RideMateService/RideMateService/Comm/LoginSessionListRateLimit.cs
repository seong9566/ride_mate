using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace RideMateService.Comm;

/// <summary>
/// 로그인 전에 부르는 계정 목록 API의 요청 빈도 제한. 기기 번호만으로 계정 이름이 보이므로 IP마다 1분에 30번까지 받는다.
/// </summary>
public static class LoginSessionListRateLimit
{
    /// <summary>
    /// 정책 이름
    /// </summary>
    public const string PolicyName = "LoginSessionList";

    private const int PermitLimit = 30;

    /// <summary>
    /// 정책을 등록한다. 넘으면 429를 돌려준다(본문 없음).
    /// </summary>
    public static IServiceCollection AddLoginSessionListRateLimit(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(PolicyName, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = PermitLimit,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                }));
        });
        return services;
    }
}
