namespace RideMateService.Comm.Dto.v1.Pickup.Response;

/// <summary>
/// 픽업 문자 발송 결과
/// </summary>
public class PickupLinkSendDto
{
    /// <summary>
    /// 409일 때만 사유 코드 (PLAN_CLOSED, NO_SPOT, NO_CHILD_PHONE, ALREADY_SENT, IN_PROGRESS, LIMIT_REACHED)
    /// </summary>
    public string? reason { get; set; }

    /// <summary>
    /// 이번 시도를 문자 사업자가 접수했는지
    /// </summary>
    public bool sent { get; set; }

    /// <summary>
    /// 남은 시도 횟수
    /// </summary>
    public int remainingAttempts { get; set; }

    /// <summary>
    /// 아이 번호 뒤 4자리. 번호를 확인하지 못했으면 null
    /// </summary>
    public string? receiverLast4 { get; set; }
}
