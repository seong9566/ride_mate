namespace RideMateService.Comm;

/// <summary>
/// 아이 픽업 안내 API의 요청 빈도 제한 값. 링크 토큰마다 센다(배포 환경은 IP 제한이 프록시 주소 하나로 묶이므로, T26).
/// 토큰을 바꿔 가며 부르는 경우를 막으려고 아이 픽업 안내 API 전체 공용 상한을 따로 둔다.
/// 두 제한은 GlobalLimiter 안에서 토큰별 → 공용 순서로 묶어 토큰별 제한을 통과한 요청만 공용 상한을 쓴다(엔드포인트 정책은 GlobalLimiter 뒤에 획득되고 고정 창 허용량은 거절돼도 돌려주지 않기 때문).
/// 등록은 Extensions/ServiceCollectionExtensions.cs의 AddRateLimitPolicies에서 한다.
/// </summary>
public static class ChildPickupRateLimit
{
    /// <summary>
    /// 조회 제한 그룹. 토큰별 파티션 키 앞에 붙여 두 API의 횟수를 따로 센다.
    /// </summary>
    public const string ViewGroup = "ChildPickupView";

    /// <summary>
    /// 토큰마다 1분에 받는 조회 요청 수
    /// </summary>
    public const int ViewPermitLimit = 30;

    /// <summary>
    /// 열람 기록 제한 그룹. 토큰별 파티션 키 앞에 붙여 두 API의 횟수를 따로 센다.
    /// </summary>
    public const string OpenGroup = "ChildPickupOpen";

    /// <summary>
    /// 토큰마다 1분에 받는 열람 기록 요청 수
    /// </summary>
    public const int OpenPermitLimit = 10;

    /// <summary>
    /// 전체 공용으로 1분에 받는 요청 수
    /// </summary>
    public const int GlobalPermitLimit = 600;

    /// <summary>
    /// 토큰별 제한의 칸 수. 토큰 해시를 이 수로 나눈 칸마다 센다. 가짜 토큰을 바꿔 가며 보내도 제한기 상태가 이 수(그룹마다)를 넘지 않는다.
    /// 같은 칸에 든 다른 링크와 토큰별 한도를 나눠 쓸 수 있다
    /// </summary>
    public const int TokenBucketCount = 4096;
}

/// <summary>아이 픽업 안내 빈도 제한을 받는 엔드포인트 표시. 그룹과 토큰별 1분 허용 횟수를 담는다</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class ChildPickupLimitAttribute : Attribute
{
    public ChildPickupLimitAttribute(string group, int permitLimit)
    {
        Group = group;
        PermitLimit = permitLimit;
    }

    /// <summary>제한 그룹(ChildPickupRateLimit.ViewGroup·OpenGroup)</summary>
    public string Group { get; }

    /// <summary>토큰마다 1분에 받는 요청 수</summary>
    public int PermitLimit { get; }
}
