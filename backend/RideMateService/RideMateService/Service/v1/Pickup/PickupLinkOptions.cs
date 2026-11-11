namespace RideMateService.Service.v1.Pickup;

/// <summary>
/// 픽업 링크 문자 설정. 설정 절 "PickupLink".
/// </summary>
public class PickupLinkOptions
{
    /// <summary>
    /// 링크 주소 앞부분(http·https 절대 URI). 뒤에 "/" + 토큰을 붙인다
    /// </summary>
    public string BaseUrl { get; set; } = "";

    /// <summary>
    /// 링크·다시 알림 각각의 시도 한도(성공 1회 + 실패 재시도)
    /// </summary>
    public int MaxAttempts { get; set; } = 3;

    /// <summary>
    /// 결과가 없는 진행 중 발송을 실패로 보기까지의 시간(초)
    /// </summary>
    public int InFlightTimeoutSec { get; set; } = 30;
}
