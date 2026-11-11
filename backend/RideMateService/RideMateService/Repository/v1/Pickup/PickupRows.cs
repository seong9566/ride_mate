namespace RideMateService.Repository.v1.Pickup;

/// <summary>
/// 오늘 픽업할 시간표 요일 한 건. EndTime은 TIME_CHANGE 예외를 반영한 하원 시각이다
/// </summary>
public class TodayPickupRow
{
    public int TimetableDayId { get; set; }
    public int AcademyId { get; set; }
    public int AreaId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public TimeSpan EndTime { get; set; }
    public decimal AcademyLat { get; set; }
    public decimal AcademyLng { get; set; }
}

/// <summary>
/// 학원의 픽업 지점 후보 한 건. WalkPathWkt는 SRID 4326 WKT라 점이 "위도 경도" 순이다
/// </summary>
public class PickupSpotRow
{
    public int PickupSpotId { get; set; }
    public string SpotName { get; set; } = string.Empty;
    public decimal Lat { get; set; }
    public decimal Lng { get; set; }
    public byte WalkMin { get; set; }
    public string? WalkPathWkt { get; set; }
}

/// <summary>
/// 픽업 지점을 쓸 수 없는 시간 한 건
/// </summary>
public class SpotBlockRow
{
    public int PickupSpotId { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public string? Reason { get; set; }
}

/// <summary>
/// 공휴일 조회 결과. YearLoaded는 그해 공휴일 행이 하나라도 있는지, IsHoliday는 그날이 공휴일인지다
/// </summary>
public class HolidayRow
{
    public bool YearLoaded { get; set; }
    public bool IsHoliday { get; set; }
}
