using System.ComponentModel.DataAnnotations;

namespace RideMateService.Comm.Dto.v1.Pickup.Request;

/// <summary>
/// 정차 요청. 서버가 배정 장소와의 거리와 정차 위치의 단속 구역을 판정한다
/// </summary>
public class UpdatePickupPlanStopDto
{
    /// <summary>
    /// 오늘 계획 ID
    /// </summary>
    [Required]
    public int? pickupPlanSeq { get; set; }

    /// <summary>
    /// 차량 이벤트 UUID(PICKUP_EVENT.CLIENT_EVENT_ID). 같은 요청을 다시 부를 때는 같은 값을, 차를 옮겨 다시 서면 새 값을 보낸다
    /// </summary>
    [Required]
    public Guid? clientEventId { get; set; }

    /// <summary>
    /// 차량이 정차한 시각(KST, 오프셋 없는 로컬 시각). 서버 현재 시각 + 1분 이하, 계획의 픽업 날짜 00:00 이후
    /// </summary>
    [Required]
    public DateTime? eventDt { get; set; }

    /// <summary>
    /// 정차 위치 위도(-90 ~ 90). 판정에만 쓰고 저장하지 않는다
    /// </summary>
    [Required]
    [Range(-90.0, 90.0)]
    public decimal? lat { get; set; }

    /// <summary>
    /// 정차 위치 경도(-180 ~ 180). 판정에만 쓰고 저장하지 않는다
    /// </summary>
    [Required]
    [Range(-180.0, 180.0)]
    public decimal? lng { get; set; }
}
