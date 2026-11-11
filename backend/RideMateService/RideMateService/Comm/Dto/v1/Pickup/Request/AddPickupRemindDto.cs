using System.ComponentModel.DataAnnotations;

namespace RideMateService.Comm.Dto.v1.Pickup.Request;

/// <summary>
/// 정차 후 다시 알림 문자 발송 요청
/// </summary>
public class AddPickupRemindDto
{
    /// <summary>
    /// 오늘 계획 ID
    /// </summary>
    [Required]
    public int? pickupPlanSeq { get; set; }

    /// <summary>
    /// 차량이 요청마다 만드는 UUID. 같은 발송을 다시 부를 때는 같은 값을 보낸다
    /// </summary>
    [Required]
    public Guid? clientEventId { get; set; }
}
