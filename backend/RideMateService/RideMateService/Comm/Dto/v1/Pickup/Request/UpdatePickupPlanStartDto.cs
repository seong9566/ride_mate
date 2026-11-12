using System.ComponentModel.DataAnnotations;

namespace RideMateService.Comm.Dto.v1.Pickup.Request;

/// <summary>
/// 경로 시작 요청. 오늘 계획이 없으면 만들면서 시작한다
/// </summary>
public class UpdatePickupPlanStartDto
{
    /// <summary>
    /// 차량 이벤트 UUID(PICKUP_EVENT.CLIENT_EVENT_ID). 같은 요청을 다시 부를 때는 같은 값을 보낸다
    /// </summary>
    [Required]
    public Guid? clientEventId { get; set; }

    /// <summary>
    /// 차량에서 출발한 시각(KST, 오프셋 없는 로컬 시각). 서버 현재 시각 + 1분 이하. 진행 중 계획이 있으면 그 계획의 시작 시각(START_DT, 없으면 픽업 날짜 00:00) 이후, 계획을 새로 만들 때는 서버 현재 시각 −10분 이후
    /// </summary>
    [Required]
    public DateTime? eventDt { get; set; }

    /// <summary>
    /// 차량 내비가 구한 픽업 장소까지 남은 시간(분). 0~300
    /// </summary>
    [Required]
    [Range(0, 300)]
    public int? driveMin { get; set; }

    /// <summary>
    /// 부모가 고른 픽업 위도. pickupLng와 함께 보낸다. 둘 다 없으면 기본 지점을 배정한다. 계획이 없을 때만 쓴다. 진행 중 계획이 있으면 무시한다
    /// </summary>
    [Range(-90.0, 90.0)]
    public decimal? pickupLat { get; set; }

    /// <summary>
    /// 부모가 고른 픽업 경도. pickupLat와 함께 보낸다. 둘 다 없으면 기본 지점을 배정한다. 계획이 없을 때만 쓴다. 진행 중 계획이 있으면 무시한다
    /// </summary>
    [Range(-180.0, 180.0)]
    public decimal? pickupLng { get; set; }
}
