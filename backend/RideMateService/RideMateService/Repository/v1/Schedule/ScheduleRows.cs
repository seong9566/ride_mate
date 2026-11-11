namespace RideMateService.Repository.v1.Schedule;

/// <summary>
/// 살아 있는 시간표의 살아 있는 요일 한 건과 그 시간표의 하원 시각
/// </summary>
public class LiveScheduleTimeRow
{
    public int TimetableId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public int DayOfWeek { get; set; }
    public TimeSpan EndTime { get; set; }
}

/// <summary>
/// 편집할 살아 있는 시간표 한 건의 학원·하원 시각
/// </summary>
public class LiveScheduleRow
{
    public int TimetableId { get; set; }
    public int AcademyId { get; set; }
    public TimeSpan EndTime { get; set; }
}

/// <summary>
/// 시간표의 요일 한 건. 삭제 표시된 행도 포함한다
/// </summary>
public class ScheduleDayRow
{
    public int TimetableDayId { get; set; }
    public int DayOfWeek { get; set; }
    public bool DeleteYn { get; set; }
}
