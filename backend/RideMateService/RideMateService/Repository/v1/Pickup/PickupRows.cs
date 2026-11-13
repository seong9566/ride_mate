namespace RideMateService.Repository.v1.Pickup;

/// <summary>
/// 오늘 픽업할 시간표 요일 한 건. EndTime은 TIME_CHANGE 예외를 반영한 하원 시각이다
/// </summary>
public class TodayPickupRow
{
    public string? AreaName { get; set; }

    public int TimetableDayId { get; set; }
    public int AcademyId { get; set; }
    public int? AreaId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public TimeSpan EndTime { get; set; }
    public decimal? AcademyLat { get; set; }
    public decimal? AcademyLng { get; set; }
    public int? PreferredSpotId { get; set; } // 시간표의 우리 픽업 장소(TIMETABLE.PICKUP_SPOT_ID)
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
/// 학원가의 요일·5분 칸별 대표 속도 한 건
/// </summary>
public sealed class TrafficProfileRow
{
    public string AreaCode { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public short SlotMin { get; set; }
    public decimal SpeedKmh { get; set; }
    public short SampleDays { get; set; }
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
    public byte Capacity { get; set; }
    public string? GuideText { get; set; }
    public string? PhotoUrl { get; set; }
    public string? Note { get; set; }
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
/// 잠근 오늘 계획 한 건. 끝난 계획은 UserId·ChildId·CustomName이 NULL이다. CustomName은 임의 좌표 계획의 장소 이름이다
/// </summary>
public class LinkPlanLockRow
{
    public int PickupPlanId { get; set; }
    public int? UserId { get; set; }
    public int? ChildId { get; set; }
    public int? PickupSpotId { get; set; }
    public string? CustomName { get; set; }
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
/// 링크 문자 받는 사람과 문구 값. ChildPhoneEnc는 아이가 없거나 삭제됐으면 null, SpotName은 지점이 없거나(임의 좌표 계획) 삭제됐으면 null이다
/// </summary>
public class LinkTargetRow
{
    public string UserName { get; set; } = string.Empty;
    public byte[]? ChildPhoneEnc { get; set; }
    public string? SpotName { get; set; }
}

/// <summary>
/// 링크 관련 이벤트(LINK_SENT·LINK_RESENT·REMIND_SENT·LINK_OPENED·CHILD_LEAVING) 한 건
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

/// <summary>
/// 오늘 계획 한 건(응답 만들기·대상 고르기용). 지점은 LEFT JOIN이라 지점이 없거나 삭제됐으면 Spot* 값이 null이다.
/// CustomWalkPathWkt는 SRID 4326 WKT라 점이 "위도 경도" 순이다. AcademyName은 시간표의 DISPLAY_NAME이다.
/// AcademyLat·AcademyLng는 계획 학원의 현재 좌표다. 끝난 계획은 UserId·AcademyId가 NULL이다.
/// ParkingLotId는 대기 장소에 도착했을 때 고른 주차장이다(없으면 null)
/// </summary>
public class LivePlanRow
{
    public string? AreaName { get; set; }

    public int PickupPlanId { get; set; }
    public int? UserId { get; set; }
    public int? TimetableDayId { get; set; }
    public int AreaId { get; set; }
    public DateOnly PickupDate { get; set; }
    public TimeSpan? EndTime { get; set; }
    public string Status { get; set; } = string.Empty;
    public int? PickupSpotId { get; set; }
    public DateTime? SlotDt { get; set; }
    public byte? WalkMin { get; set; }
    public DateTime ExpireDt { get; set; }
    public decimal? CustomLat { get; set; }
    public decimal? CustomLng { get; set; }
    public string? CustomName { get; set; }
    public string? CustomWalkPathWkt { get; set; }
    public string? SpotName { get; set; }
    public decimal? SpotLat { get; set; }
    public decimal? SpotLng { get; set; }
    public string? GuideText { get; set; }
    public string? PhotoUrl { get; set; }
    public string? AcademyName { get; set; }
    public int? AcademyId { get; set; }
    public decimal? AcademyLat { get; set; }
    public decimal? AcademyLng { get; set; }
    public int? ParkingLotId { get; set; }
    public int? PreferredSpotId { get; set; } // 계획 시간표의 우리 픽업 장소(TIMETABLE.PICKUP_SPOT_ID)
}

/// <summary>
/// 잠근 시간표 요일 한 건. EndTime은 그날 TIME_CHANGE를 반영한 하원 시각, Skipped는 그날 SKIP 예외가 있는지,
/// Alive는 요일·시간표·아이가 모두 삭제되지 않았는지다. AreaId·AcademyLat·AcademyLng는 학원의 현재 값이다
/// </summary>
public class TimetableDayLockRow
{
    public int TimetableDayId { get; set; }
    public int UserId { get; set; }
    public int ChildId { get; set; }
    public int AcademyId { get; set; }
    public int? AreaId { get; set; }
    public decimal? AcademyLat { get; set; }
    public decimal? AcademyLng { get; set; }
    public bool PickupYn { get; set; }
    public DateOnly? LastPickupDate { get; set; }
    public TimeSpan EndTime { get; set; }
    public bool Skipped { get; set; }
    public bool Alive { get; set; }
    public int? PreferredSpotId { get; set; } // 시간표의 우리 픽업 장소(TIMETABLE.PICKUP_SPOT_ID)
}

/// <summary>
/// 픽업 장소 후보 목록 대상 시간표 한 건. EndTime은 그 날짜 요일의 TIME_CHANGE 예외를 반영한 하원 시각이고,
/// TimetableDayId는 그 날짜 요일의 살아 있는 요일 행이다(없으면 null). AreaId·AreaName·AcademyLat·AcademyLng는 학원의 현재 값이다
/// </summary>
public class PickupSpotTargetRow
{
    public int TimetableId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public int AcademyId { get; set; }
    public int? AreaId { get; set; }
    public string? AreaName { get; set; }
    public decimal? AcademyLat { get; set; }
    public decimal? AcademyLng { get; set; }
    public int? PreferredSpotId { get; set; }
    public TimeSpan EndTime { get; set; }
    public int? TimetableDayId { get; set; }
}

/// <summary>
/// 재배정을 위해 잠근 오늘 계획 한 건. 끝난 계획은 UserId·AcademyId·EndTime이 NULL이다
/// </summary>
public class PlanLockRow
{
    public int PickupPlanId { get; set; }
    public int? UserId { get; set; }
    public int? AcademyId { get; set; }
    public int AreaId { get; set; }
    public DateOnly PickupDate { get; set; }
    public TimeSpan? EndTime { get; set; }
    public string Status { get; set; } = string.Empty;
    public int? PickupSpotId { get; set; }
    public DateTime? SlotDt { get; set; }
    public decimal? CustomLat { get; set; }
    public DateTime ExpireDt { get; set; }
    public int? TimetableDayId { get; set; }
}

/// <summary>
/// 끝낼 오늘 계획의 요일 참조와 날짜. Live는 STATUS가 아직 끝나지 않았는지다
/// </summary>
public class ClosePlanRow
{
    public int? TimetableDayId { get; set; }
    public DateOnly PickupDate { get; set; }
    public bool Live { get; set; }
}

/// <summary>
/// 취소 문자 받는 사람과 문구 값. ChildPhoneEnc는 아이가 없거나 삭제됐으면 null이다
/// </summary>
public class CancelNoticeTargetRow
{
    public string UserName { get; set; } = string.Empty;
    public byte[]? ChildPhoneEnc { get; set; }
}

/// <summary>
/// 차량 이벤트 번호로 찾은 이벤트 한 건(재요청 응답 만들기용)
/// </summary>
public class PlanEventRow
{
    public string EventType { get; set; } = string.Empty;
    public string? Detail { get; set; }
}

/// <summary>
/// 아이 픽업 안내에 쓰는 운전자와 차량 정보
/// </summary>
public class ChildPickupDriverRow
{
    /// <summary>
    /// 운전자 이름
    /// </summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// 차종
    /// </summary>
    public string? CarModel { get; set; }

    /// <summary>
    /// 차 색
    /// </summary>
    public string? CarColor { get; set; }

    /// <summary>
    /// 차량 번호 뒷자리 4자리
    /// </summary>
    public string? CarNoLast4 { get; set; }
}

/// <summary>
/// 위치 보고 대상 계획
/// </summary>
public class PositionPlanRow
{
    /// <summary>
    /// 소유 사용자 ID. 익명화되었으면 null
    /// </summary>
    public int? UserId { get; set; }

    /// <summary>
    /// 계획 상태
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// 계획 만료 시각
    /// </summary>
    public DateTime ExpireDt { get; set; }
}

/// <summary>
/// 픽업 장소 추가 요청 대상 시간표의 학원
/// </summary>
public class SpotRequestScheduleRow
{
    /// <summary>
    /// 학원 ID
    /// </summary>
    public int AcademyId { get; set; }

    /// <summary>
    /// 학원가 ID. 학원에 좌표가 없으면 null
    /// </summary>
    public int? AreaId { get; set; }
}

/// <summary>
/// 오늘의 픽업 경로에 쓰는 시간표 요일 한 건. EndTime은 TIME_CHANGE 예외를 반영한 하원 시각이다
/// </summary>
public class TodayPickupRouteRow
{
    public string ChildName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public TimeSpan EndTime { get; set; }
    public string? AreaName { get; set; }
    public string AcademyName { get; set; } = string.Empty;
    public string AcademyAddress { get; set; } = string.Empty;
    public decimal? AcademyLat { get; set; }
    public decimal? AcademyLng { get; set; }
}

/// <summary>
/// 계정의 저장된 집. 집을 저장하지 않았으면 DestAddress가 null이다
/// </summary>
public class UserHomeRow
{
    public string? DestAddress { get; set; }
    public decimal? DestLat { get; set; }
    public decimal? DestLng { get; set; }
}
