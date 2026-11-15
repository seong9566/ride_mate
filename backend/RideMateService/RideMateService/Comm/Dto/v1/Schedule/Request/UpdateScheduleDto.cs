using System.ComponentModel.DataAnnotations;

namespace RideMateService.Comm.Dto.v1.Schedule.Request;

/// <summary>
/// 시간표 편집 요청. 등록 요청의 필드를 모두 보내 시간표 전체를 바꾼다
/// </summary>
public class UpdateScheduleDto : AddScheduleDto
{
    /// <summary>
    /// 바꿀 시간표 ID. 시간표 목록의 scheduleSeq
    /// </summary>
    [Required]
    public int? scheduleSeq { get; set; }
}
