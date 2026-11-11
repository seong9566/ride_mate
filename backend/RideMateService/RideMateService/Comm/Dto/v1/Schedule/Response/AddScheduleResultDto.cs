namespace RideMateService.Comm.Dto.v1.Schedule.Response;

/// <summary>
/// 시간표 등록 결과. 200이면 schedule만, 103(겹침 경고)이면 overlaps만 채워진다
/// </summary>
public class AddScheduleResultDto
{
    /// <summary>
    /// 저장한 시간표. 200일 때만 채워진다
    /// </summary>
    public ScheduleItemDto? schedule { get; set; }

    /// <summary>
    /// 같은 요일에 하원 시각이 40분 이하로 가까운 기존 시간표. 103일 때만 채워진다
    /// </summary>
    public List<ScheduleOverlapItemDto>? overlaps { get; set; }
}

/// <summary>
/// 겹친 기존 시간표 한 건(요일마다 한 건)
/// </summary>
public class ScheduleOverlapItemDto
{
    /// <summary>
    /// 시간표 ID
    /// </summary>
    public int scheduleSeq { get; set; }

    /// <summary>
    /// 부모가 지은 학원 이름
    /// </summary>
    public string displayName { get; set; } = string.Empty;

    /// <summary>
    /// 겹친 요일. 1(월)~7(일)
    /// </summary>
    public int dayOfWeek { get; set; }

    /// <summary>
    /// 기존 시간표의 하원 시각. HH:mm
    /// </summary>
    public string endTime { get; set; } = string.Empty;
}
