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
