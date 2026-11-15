using System.ComponentModel.DataAnnotations;

namespace RideMateService.Comm.Dto.v1.Pickup.Request;

/// <summary>
/// 픽업 장소 후보 목록 요청
/// </summary>
public class PickupSpotListQueryDto
{
    /// <summary>
    /// 후보를 볼 시간표 ID. 시간표 목록의 scheduleSeq
    /// </summary>
    [Required]
    [Range(1, int.MaxValue)]
    public int? scheduleSeq { get; set; }
}
