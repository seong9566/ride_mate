namespace RideMateService.Repository.v1.Pickup;

/// <summary>
/// 오늘 픽업할 시간표 요일 한 건. EndTime은 TIME_CHANGE 예외를 반영한 하원 시각이다
/// </summary>
public class TodayPickupRow
{
    public int TimetableDayId { get; set; }
    public int AcademyId { get; set; }
    public int? AreaId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public TimeSpan EndTime { get; set; }
    public decimal? AcademyLat { get; set; }
    public decimal? AcademyLng { get; set; }
}

/// <summary>
/// 대기 장소 추천 계산에 쓰는 주차장 한 건
/// </summary>
public class ParkingLotRow
{
    public int ParkingLotId { get; set; }
    public string LotName { get; set; } = string.Empty;
    public decimal Lat { get; set; }
    public decimal Lng { get; set; }
    public TimeSpan? WeekdayOpenTime { get; set; }
    public TimeSpan? WeekdayCloseTime { get; set; }
    public TimeSpan? SatOpenTime { get; set; }
    public TimeSpan? SatCloseTime { get; set; }
    public TimeSpan? HolidayOpenTime { get; set; }
    public TimeSpan? HolidayCloseTime { get; set; }
    public short BaseMin { get; set; }
    public int BaseFee { get; set; }
    public short? AddUnitMin { get; set; }
    public int? AddFee { get; set; }
    public int? DayPassFee { get; set; }
    public int? DayMaxFee { get; set; }
    public string? RealtimeRef { get; set; }
    public short? AvailableSpaces { get; set; }
    public DateTime? AvailableUpdateDt { get; set; }
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

/// <summary>
/// 로그인한 부모가 이용하는 학원가 한 건
/// </summary>
public class UserAreaRow
{
    public int AreaId { get; set; }
    public string AreaName { get; set; } = string.Empty;
    public DateTime AreaUpdatedDt { get; set; }
}

/// <summary>
/// 단속 구역 목록 버전 계산에 쓰는 변경 시각과 살아 있는 행 수
/// </summary>
public class ZoneVersionRow
{
    public DateTime? MaxDt { get; set; }
    public int LiveCount { get; set; }
}

/// <summary>
/// 단속 구역 한 건. GeomWkt는 경도·위도 순서다
/// </summary>
public class ZoneRow
{
    public int RestrictedZoneId { get; set; }
    public int AreaId { get; set; }
    public string ZoneType { get; set; } = string.Empty;
    public string GeomWkt { get; set; } = string.Empty;
    public short BufferM { get; set; }
    public string? DayType { get; set; }
    public TimeSpan? ActiveStartTime { get; set; }
    public TimeSpan? ActiveEndTime { get; set; }
}

/// <summary>
/// 잠근 오늘 계획 한 건. 끝난 계획은 UserId·ChildId가 NULL이다
/// </summary>
public class LinkPlanLockRow
{
    public int PickupPlanId { get; set; }
    public int? UserId { get; set; }
    public int? ChildId { get; set; }
    public int? PickupSpotId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime ExpireDt { get; set; }
}

/// <summary>
/// 전달 상태 조회용 오늘 계획 한 건
/// </summary>
public class LinkStatusPlanRow
{
    public string Status { get; set; } = string.Empty;
    public DateTime ExpireDt { get; set; }
}

/// <summary>
/// 링크 문자 받는 사람과 문구 값. ChildPhoneEnc는 아이가 없거나 삭제됐으면 null, SpotName은 지점이 삭제됐으면 null이다
/// </summary>
public class LinkTargetRow
{
    public string UserName { get; set; } = string.Empty;
    public byte[]? ChildPhoneEnc { get; set; }
    public string? SpotName { get; set; }
}

/// <summary>
/// 링크 관련 이벤트(LINK_SENT·REMIND_SENT·LINK_OPENED·CHILD_LEAVING) 한 건
/// </summary>
public class LinkEventRow
{
    public int PickupEventId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public DateTime EventDt { get; set; }
    public string? ClientEventId { get; set; }
    public string? Detail { get; set; }
    public DateTime CreateDt { get; set; }
}

/// <summary>
/// 아이 출발 요청용 픽업 세션 한 건
/// </summary>
public class LeaveSessionRow
{
    public int PickupPlanId { get; set; }
    public DateTime? CloseDt { get; set; }
}
