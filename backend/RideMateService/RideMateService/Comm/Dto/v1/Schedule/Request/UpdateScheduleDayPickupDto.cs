using System.ComponentModel.DataAnnotations;

namespace RideMateService.Comm.Dto.v1.Schedule.Request;

/// <summary>
/// 요일별 픽업 스위치 변경 요청
/// </summary>
public class UpdateScheduleDayPickupDto
{
    /// <summary>
    /// 바꿀 시간표 요일 ID. 시간표 목록 days의 scheduleDaySeq
    /// </summary>
    [Required]
    public int? scheduleDaySeq { get; set; }

    /// <summary>
    /// 그 요일의 픽업 스위치. 켜면 true
    /// </summary>
    [Required]
    public bool? pickupYn { get; set; }
}
