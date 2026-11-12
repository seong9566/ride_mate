using System.ComponentModel.DataAnnotations;

namespace RideMateService.Comm.Dto.v1.Pickup.Request;

/// <summary>
/// 부모 취소 요청. 오늘 계획을 CANCELED로 끝낸다
/// </summary>
public class DeletePickupPlanDto
{
    /// <summary>
    /// 오늘 계획 ID
    /// </summary>
    [Required]
    public int? pickupPlanSeq { get; set; }

    /// <summary>
    /// 차량 이벤트 UUID(PICKUP_EVENT.CLIENT_EVENT_ID). 같은 요청을 다시 부를 때는 같은 값을 보낸다
    /// </summary>
    [Required]
    public Guid? clientEventId { get; set; }
}
