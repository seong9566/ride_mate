using System.ComponentModel.DataAnnotations;

namespace RideMateService.Comm.Dto.v1.Pickup.Request;

/// <summary>
/// 픽업 완료 기록 조회 요청
/// </summary>
public class DetailPickupRecordQueryDto
{
    /// <summary>
    /// 조회할 시간표 ID. 시간표 목록의 scheduleSeq
    /// </summary>
    [Required]
    [Range(1, int.MaxValue)]
    public int? scheduleSeq { get; set; }

    /// <summary>
    /// 조회할 요일. 1(월)~7(일)
    /// </summary>
    [Required]
    [Range(1, 7)]
    public int? dayOfWeek { get; set; }
}
