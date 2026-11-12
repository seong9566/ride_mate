using System.ComponentModel.DataAnnotations;

namespace RideMateService.Comm.Dto.v1.Pickup.Request;

/// <summary>
/// 픽업 장소 바꾸기 요청. 좌표가 둘 다 없으면 기본 지점으로 되돌린다
/// </summary>
public class UpdatePickupPlanLocationDto
{
    /// <summary>
    /// 오늘 계획 ID
    /// </summary>
    [Required]
    public int? pickupPlanSeq { get; set; }

    /// <summary>
    /// 차량이 요청마다 만드는 UUID. 같은 요청을 다시 부를 때는 같은 값을 보낸다
    /// </summary>
    [Required]
    public Guid? clientEventId { get; set; }

    /// <summary>
    /// 새 픽업 위도. pickupLng와 함께 보낸다. 둘 다 없으면 기본 지점으로 되돌린다
    /// </summary>
    [Range(-90.0, 90.0)]
    public decimal? pickupLat { get; set; }

    /// <summary>
    /// 새 픽업 경도. pickupLat와 함께 보낸다. 둘 다 없으면 기본 지점으로 되돌린다
    /// </summary>
    [Range(-180.0, 180.0)]
    public decimal? pickupLng { get; set; }
}
