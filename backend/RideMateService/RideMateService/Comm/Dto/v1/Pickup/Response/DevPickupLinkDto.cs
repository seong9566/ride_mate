namespace RideMateService.Comm.Dto.v1.Pickup.Response;

/// <summary>
/// 개발 전용 픽업 링크 발급 결과
/// </summary>
public class DevPickupLinkDto
{
    /// <summary>
    /// 409일 때만 사유 코드 (PLAN_CLOSED, NO_SPOT)
    /// </summary>
    public string? reason { get; set; }

    /// <summary>
    /// 오늘 계획 ID
    /// </summary>
    public int pickupPlanSeq { get; set; }

    /// <summary>
    /// 링크 토큰(43자). 아이 웹 API의 X-Pickup-Token 헤더에 넣는다
    /// </summary>
    public string? token { get; set; }

    /// <summary>
    /// 아이에게 보내는 문자와 같은 링크 주소
    /// </summary>
    public string? url { get; set; }

    /// <summary>
    /// 링크가 끝나는 시각(계획 만료 시각). 9999-12-31 23:59:59
    /// </summary>
    public DateTime? expireDt { get; set; }
}
