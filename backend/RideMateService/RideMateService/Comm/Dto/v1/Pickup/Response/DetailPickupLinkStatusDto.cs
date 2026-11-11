namespace RideMateService.Comm.Dto.v1.Pickup.Response;

/// <summary>
/// 픽업 링크 전달 상태
/// </summary>
public class DetailPickupLinkStatusDto
{
    /// <summary>
    /// 링크 발송 상태 (NONE: 시도 없음, SENDING: 진행 중, SENT: 접수됨, FAILED: 마지막 시도 실패)
    /// </summary>
    public string linkStatus { get; set; } = string.Empty;

    /// <summary>
    /// 링크 발송이 접수된 시각. 접수된 발송이 없으면 null
    /// </summary>
    public DateTime? sentDt { get; set; }

    /// <summary>
    /// 링크 발송의 남은 시도 횟수
    /// </summary>
    public int remainingAttempts { get; set; }

    /// <summary>
    /// 아이가 링크를 열었는지
    /// </summary>
    public bool opened { get; set; }

    /// <summary>
    /// 아이가 링크를 처음 연 시각. 열지 않았으면 null
    /// </summary>
    public DateTime? openedDt { get; set; }

    /// <summary>
    /// 아이가 "지금 나가요"를 눌렀는지
    /// </summary>
    public bool leaving { get; set; }

    /// <summary>
    /// 아이가 "지금 나가요"를 처음 누른 시각. 누르지 않았으면 null
    /// </summary>
    public DateTime? leavingDt { get; set; }

    /// <summary>
    /// 다시 알림 발송 상태 (NONE, SENDING, SENT, FAILED)
    /// </summary>
    public string remindStatus { get; set; } = string.Empty;
}
