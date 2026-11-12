using System.ComponentModel.DataAnnotations;

namespace RideMateService.Comm.Dto.v1.Pickup.Request;

/// <summary>
/// 오늘 픽업 계획 만들기 요청
/// </summary>
public class AddPickupPlanDto
{
    /// <summary>
    /// 부모가 고른 픽업 위도. pickupLng와 함께 보낸다. 둘 다 없으면 기본 지점을 배정한다
    /// </summary>
    [Range(-90.0, 90.0)]
    public decimal? pickupLat { get; set; }

    /// <summary>
    /// 부모가 고른 픽업 경도. pickupLat와 함께 보낸다. 둘 다 없으면 기본 지점을 배정한다
    /// </summary>
    [Range(-180.0, 180.0)]
    public decimal? pickupLng { get; set; }
}
