namespace RideMateService.Comm;

/// <summary>
/// 아이 픽업 안내 API의 요청 빈도 제한 값. 링크 토큰마다 센다(배포 환경은 IP 제한이 프록시 주소 하나로 묶이므로, T26).
/// 토큰을 바꿔 가며 부르는 경우를 막으려고 아이 픽업 안내 API 전체 공용 상한을 따로 둔다(GlobalPolicyNames).
/// 등록은 Extensions/ServiceCollectionExtensions.cs의 AddRateLimitPolicies에서 한다.
/// </summary>
public static class ChildPickupRateLimit
{
    /// <summary>
    /// 조회 정책 이름
    /// </summary>
    public const string ViewPolicyName = "ChildPickupView";

    /// <summary>
    /// 토큰마다 1분에 받는 조회 요청 수
    /// </summary>
    public const int ViewPermitLimit = 30;

    /// <summary>
    /// 열람 기록 정책 이름
    /// </summary>
    public const string OpenPolicyName = "ChildPickupOpen";

    /// <summary>
    /// 토큰마다 1분에 받는 열람 기록 요청 수
    /// </summary>
    public const int OpenPermitLimit = 10;

    /// <summary>
    /// 전체 공용으로 1분에 받는 요청 수
    /// </summary>
    public const int GlobalPermitLimit = 600;

    /// <summary>
    /// 공용 상한을 받는 정책 이름 목록
    /// </summary>
    public static readonly IReadOnlySet<string> GlobalPolicyNames = new HashSet<string> { ViewPolicyName, OpenPolicyName };
}
