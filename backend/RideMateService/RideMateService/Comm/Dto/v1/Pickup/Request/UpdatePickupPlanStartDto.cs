using System.ComponentModel.DataAnnotations;

namespace RideMateService.Comm.Dto.v1.Pickup.Request;

/// <summary>
/// 경로 시작 요청. 늦게 출발했으면 배정 시각을 차 도착 예상 시각으로 미룬다
/// </summary>
public class UpdatePickupPlanStartDto
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
    /// 차량에서 출발한 시각(KST, 오프셋 없는 로컬 시각). 서버 현재 시각 기준 -10분 ~ +1분
    /// </summary>
    [Required]
    public DateTime? eventDt { get; set; }

    /// <summary>
    /// 차량 내비가 구한 픽업 장소까지 남은 시간(분). 0~300
    /// </summary>
    [Required]
    [Range(0, 300)]
    public int? driveMin { get; set; }
}
