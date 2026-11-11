namespace RideMateService.Comm.Dto.v1.Schedule.Response;

/// <summary>
/// 시간표 삭제 결과
/// </summary>
public class DeleteScheduleResultDto
{
    /// <summary>
    /// 지운 시간표 ID
    /// </summary>
    public int scheduleSeq { get; set; }
}
