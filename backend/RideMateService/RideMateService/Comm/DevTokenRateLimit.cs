namespace RideMateService.Comm;

/// <summary>
/// 개발 전용 토큰 발급 API의 요청 빈도 제한 값. 배포 서버에서도 열려 있으므로 클라이언트마다 1분에 30번까지 받는다.
/// 프록시 뒤에서 UseForwardedHeaders가 없으면 모든 요청이 한 파티션으로 묶인다(T26).
/// 등록은 Extensions/ServiceCollectionExtensions.cs의 AddRateLimitPolicies에서 한다.
/// </summary>
public static class DevTokenRateLimit
{
    /// <summary>
    /// 정책 이름
    /// </summary>
    public const string PolicyName = "DevToken";

    /// <summary>
    /// 1분에 받는 요청 수
    /// </summary>
    public const int PermitLimit = 30;
}
