namespace RideMateService.Comm;

/// <summary>
/// 차량 픽업 링크 전달 상태 실시간 연결의 요청 빈도 제한 값. 사용자마다 1분에 20번 연결을 받는다(여러 계획과 토큰 갱신 뒤 재연결을 감안한 값).
/// 등록은 Extensions/ServiceCollectionExtensions.cs의 AddRateLimitPolicies에서 한다.
/// </summary>
public static class PickupLinkStreamRateLimit
{
    /// <summary>
    /// 정책 이름
    /// </summary>
    public const string PolicyName = "PickupLinkStream";

    /// <summary>
    /// 1분에 받는 연결 시도 수
    /// </summary>
    public const int PermitLimit = 20;
}
