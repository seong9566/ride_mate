namespace RideMateService.Comm.Dto.v1.Pickup.Request;

/// <summary>
/// 아이 출발 알림 요청
/// </summary>
public class AddChildLeaveDto
{
    /// <summary>
    /// 출발 예정 시각. null이면 지금 나가요 요청이다
    /// </summary>
    public DateTime? leaveDt { get; set; }
}
