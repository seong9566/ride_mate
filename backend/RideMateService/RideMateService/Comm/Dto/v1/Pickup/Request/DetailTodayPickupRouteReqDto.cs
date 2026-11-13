using System.ComponentModel.DataAnnotations;

namespace RideMateService.Comm.Dto.v1.Pickup.Request;

/// <summary>
/// 오늘의 픽업 경로 조회 요청
/// </summary>
public class TodayPickupRouteQueryDto
{
    /// <summary>
    /// 고른 시간표 ID(이번 주 스케줄의 scheduleSeq)
    /// </summary>
    [Required]
    [Range(1, int.MaxValue)]
    public int? scheduleSeq { get; set; }

    /// <summary>
    /// 고른 요일. 1(월)~7(일). 서버의 오늘 요일과 다르면 code 101이다
    /// </summary>
    [Required]
    [Range(1, 7)]
    public int? dayOfWeek { get; set; }
}
