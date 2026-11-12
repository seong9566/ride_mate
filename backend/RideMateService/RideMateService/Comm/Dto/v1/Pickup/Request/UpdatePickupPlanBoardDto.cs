using System.ComponentModel.DataAnnotations;

namespace RideMateService.Comm.Dto.v1.Pickup.Request;

/// <summary>
/// 탑승 요청. 아이가 탄 것을 기록하고 오늘 계획을 끝낸다
/// </summary>
public class UpdatePickupPlanBoardDto
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

    /// <summary>
    /// 차량이 탑승을 감지한 시각(KST, 오프셋 없는 로컬 시각). 서버 현재 시각 + 1분 이하, 계획의 픽업 날짜 00:00 이후
    /// </summary>
    [Required]
    public DateTime? eventDt { get; set; }
}
