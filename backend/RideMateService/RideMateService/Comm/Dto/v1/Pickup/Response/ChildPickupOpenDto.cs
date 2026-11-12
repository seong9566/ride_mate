namespace RideMateService.Comm.Dto.v1.Pickup.Response;

/// <summary>
/// 아이 픽업 링크 열람 기록
/// </summary>
public class ChildPickupOpenDto
{
    /// <summary>
    /// 409일 때만. LINK_CLOSED(링크가 끝남)
    /// </summary>
    public string? reason { get; set; }

    /// <summary>
    /// 아이가 링크를 처음 연 시각(첫 LINK_OPENED의 EVENT_DT). 409면 null
    /// </summary>
    public DateTime? openedDt { get; set; }
}
