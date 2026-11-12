namespace RideMateService.Comm;

/// <summary>
/// 픽업 장소 추가 요청 API의 요청 빈도 제한 값. 사용자(JWT sub)마다 1시간에 5건까지 받는다. 운영팀이 DB에서 직접 보는 요청이라 스팸을 막는다.
/// 등록은 Extensions/ServiceCollectionExtensions.cs의 AddRateLimitPolicies에서 한다.
/// </summary>
public static class PickupSpotRequestRateLimit
{
    /// <summary>
    /// 정책 이름
    /// </summary>
    public const string PolicyName = "PickupSpotRequest";

    /// <summary>
    /// 1시간에 받는 요청 수
    /// </summary>
    public const int PermitLimit = 5;
}
