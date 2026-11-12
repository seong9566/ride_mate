namespace RideMateService.Comm;

/// <summary>
/// 차 위치 보고 API의 요청 빈도 제한 값. 사용자마다 1분에 60번까지 받는다(3초 보고는 20회).
/// 등록은 Extensions/ServiceCollectionExtensions.cs의 AddRateLimitPolicies에서 한다.
/// </summary>
public static class PickupPositionRateLimit
{
    /// <summary>
    /// 정책 이름
    /// </summary>
    public const string PolicyName = "PickupPosition";

    /// <summary>
    /// 1분에 받는 요청 수
    /// </summary>
    public const int PermitLimit = 60;
}
