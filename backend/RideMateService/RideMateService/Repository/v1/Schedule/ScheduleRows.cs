namespace RideMateService.Repository.v1.Schedule;

/// <summary>
/// 운전자 계정의 집과 LEFT JOIN한 살아 있는 아이 한 명. 아이가 없으면 아이 칸은 모두 null이다
/// </summary>
public class ScheduleHomeRow
{
    public string? DestAddress { get; set; }
    public decimal? DestLat { get; set; }
    public decimal? DestLng { get; set; }
    public int? ChildId { get; set; }
    public string? ChildName { get; set; }
}

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
    public int? PickupSpotId { get; set; } // 우리 픽업 장소(TIMETABLE.PICKUP_SPOT_ID)
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
