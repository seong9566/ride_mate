namespace RideMateService.Comm.Dto.v1.Pickup.Response;

/// <summary>
/// 아이 출발 알림 상태
/// </summary>
public class ChildLeaveDto
{
    /// <summary>
    /// 409일 때만 사유 코드 (LINK_CLOSED, ALREADY_LEAVING)
    /// </summary>
    public string? reason { get; set; }

    /// <summary>
    /// 출발 상태 (NONE, SCHEDULED, SOON, LEAVING)
    /// </summary>
    public string leaveStatus { get; set; } = string.Empty;

    /// <summary>
    /// 예정 출발 시각. SCHEDULED·SOON일 때만 값이 있다
    /// </summary>
    public DateTime? plannedLeaveDt { get; set; }

    /// <summary>
    /// 처음 "지금 나가요"를 누른 시각
    /// </summary>
    public DateTime? leavingDt { get; set; }
}
