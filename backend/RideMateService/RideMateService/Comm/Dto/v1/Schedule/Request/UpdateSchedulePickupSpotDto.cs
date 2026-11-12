using System.ComponentModel.DataAnnotations;

namespace RideMateService.Comm.Dto.v1.Schedule.Request;

/// <summary>
/// 우리 픽업 장소 저장 요청
/// </summary>
public class UpdateSchedulePickupSpotDto
{
    /// <summary>
    /// 바꿀 시간표 ID. 시간표 목록의 scheduleSeq
    /// </summary>
    [Required]
    [Range(1, int.MaxValue)]
    public int? scheduleSeq { get; set; }

    /// <summary>
    /// 우리 픽업 장소로 정할 픽업 지점 ID. 없거나 null이면 우리 픽업 장소를 지우고 우선순위 기본으로 되돌린다
    /// </summary>
    [Range(1, int.MaxValue)]
    public int? pickupSpotSeq { get; set; }
}
