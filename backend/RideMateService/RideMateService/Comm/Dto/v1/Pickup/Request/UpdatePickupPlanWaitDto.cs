using System.ComponentModel.DataAnnotations;

namespace RideMateService.Comm.Dto.v1.Pickup.Request;

/// <summary>
/// 대기 장소 도착 요청. 고른 주차장에 도착했음을 기록하고 계획을 WAITING으로 바꾼다
/// </summary>
public class UpdatePickupPlanWaitDto
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
    /// 차량이 대기 장소 도착을 판정한 시각(KST, 오프셋 없는 로컬 시각). 서버 현재 시각 + 1분 이하, 계획의 픽업 날짜 00:00 이후
    /// </summary>
    [Required]
    public DateTime? eventDt { get; set; }

    /// <summary>
    /// 도착한 주차장 ID(대기 장소 추천의 parkingLotSeq). 계획과 같은 학원가의 살아 있는 주차장이어야 한다
    /// </summary>
    [Required]
    [Range(1, int.MaxValue)]
    public int? parkingLotSeq { get; set; }
}
