using System.ComponentModel.DataAnnotations;

namespace RideMateService.Comm.Dto.v1.Pickup.Request;

/// <summary>
/// 차 위치·내비 남은 시간 보고. 차량은 경로 시작부터 탑승 전까지 3초마다 보낸다. 오프라인 대기열에 쌓아 다시 보내지 않는다
/// </summary>
public class UpdatePickupPlanPositionDto
{
    /// <summary>
    /// 오늘 계획 ID
    /// </summary>
    [Required]
    public int? pickupPlanSeq { get; set; }

    /// <summary>
    /// 차 위도. DB에 저장하지 않고 메모리에만 둔다
    /// </summary>
    [Required]
    [Range(-90.0, 90.0)]
    public decimal? lat { get; set; }

    /// <summary>
    /// 차 경도. DB에 저장하지 않고 메모리에만 둔다
    /// </summary>
    [Required]
    [Range(-180.0, 180.0)]
    public decimal? lng { get; set; }

    /// <summary>
    /// 내비 남은 시간(초). 내비 목적지가 이 계획의 픽업 장소일 때만 보낸다. 대기 장소로 안내 중이거나 경로 안내가 없으면 비운다
    /// </summary>
    [Range(0, 10800)]
    public int? etaSec { get; set; }
}
