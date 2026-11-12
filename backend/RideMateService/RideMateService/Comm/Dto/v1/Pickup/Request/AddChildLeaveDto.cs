namespace RideMateService.Comm.Dto.v1.Pickup.Request;

/// <summary>
/// 아이 출발 알림 요청
/// </summary>
public class AddChildLeaveDto
{
    /// <summary>
    /// 지금 나가요. true일 때만 지금 나가요로 기록한다. leaveDt와 함께 보낼 수 없다
    /// </summary>
    public bool? leaveNow { get; set; }

    /// <summary>
    /// 출발 예정 시각(오프셋 없는 KST). 설정·변경할 때 보낸다. leaveNow와 함께 보낼 수 없다
    /// </summary>
    public DateTime? leaveDt { get; set; }
}
