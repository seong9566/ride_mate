using System.ComponentModel.DataAnnotations;

namespace RideMateService.Comm.Dto.v1.Schedule.Request;

/// <summary>
/// 시간표 삭제 요청
/// </summary>
public class DeleteScheduleDto
{
    /// <summary>
    /// 지울 시간표 ID. 시간표 목록의 scheduleSeq
    /// </summary>
    [Required]
    public int? scheduleSeq { get; set; }
}
