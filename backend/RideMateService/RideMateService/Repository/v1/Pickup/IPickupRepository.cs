using System.Data;
using RideMateService.Commons.DBModels;

namespace RideMateService.Repository.v1.Pickup;

public interface IPickupRepository
{
    /// <summary>
    /// 오늘 계획의 픽업 세션을 불러온다. 끝난 계획의 세션도 돌려준다
    /// </summary>
    Task<PickupSession?> GetPickupSessionAsync(int pickupPlanId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 픽업 세션 등록
    /// </summary>
    Task<bool> AddPickupSessionAsync(PickupSession model, CancellationToken cancellationToken = default);

    /// <summary>
    /// 픽업 이벤트를 PK로 불러온다(수정 대상). 끝난 계획의 이벤트도 돌려준다
    /// </summary>
    Task<PickupEvent?> GetPickupEventAsync(int pickupEventId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 픽업 이벤트 등록. 저장 뒤 model의 픽업 이벤트 ID가 채워진다
    /// </summary>
    Task<bool> AddPickupEventAsync(PickupEvent model, CancellationToken cancellationToken = default);

    /// <summary>
    /// 픽업 이벤트 수정
    /// </summary>
    Task<bool> UpdatePickupEventAsync(PickupEvent model, CancellationToken cancellationToken = default);

    /// <summary>
    /// 오늘 계획 등록. 저장 뒤 model의 오늘 계획 ID가 채워진다.
    /// 같은 요일·날짜의 계획이 이미 있으면 MySQL 중복 키 예외(1062, DbUpdateException 안쪽)를 로그 없이 올려보낸다
    /// </summary>
    Task<bool> AddPickupPlanAsync(PickupPlan model, CancellationToken cancellationToken = default);

    /// <summary>
    /// 오늘 계획을 PK로 불러온다(수정 대상, 추적). 끝난 계획도 돌려준다
    /// </summary>
    Task<PickupPlan?> GetPickupPlanEntityAsync(int pickupPlanId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 오늘 계획 수정
    /// </summary>
    Task<bool> UpdatePickupPlanAsync(PickupPlan model, CancellationToken cancellationToken = default);

    // ====== Dapper

    /// <summary>
    /// 로그인한 부모의 살아 있는 아이 시간표 중 그날 요일에 픽업하는 것을 조회한다.
    /// 그날 SKIP 예외는 빼고, TIME_CHANGE 예외는 바뀐 하원 시각을 EndTime으로 돌려준다. 그날 이미 픽업을 마친 것은 뺀다
    /// </summary>
    Task<List<TodayPickupRow>> GetTodayPickupsAsync(int userId, DateOnly date, byte dayOfWeek,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 학원의 픽업 지점 후보 중 사용 중인 것을 우선순위 순으로 조회한다
    /// </summary>
    Task<List<PickupSpotRow>> GetAcademyPickupSpotsAsync(int academyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 픽업 지점들의 그 요일 유형 금지 시간을 조회한다
    /// </summary>
    Task<List<SpotBlockRow>> GetPickupSpotBlocksAsync(int[] pickupSpotIds, string dayType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 그날이 속한 해의 공휴일 적재 여부와 그날의 공휴일 여부를 조회한다
    /// </summary>
    Task<HolidayRow> GetHolidayAsync(DateOnly date, CancellationToken cancellationToken = default);

    /// <summary>
    /// 좌표가 학원가 경계 안인지 조회한다. 학원가가 없으면 false
    /// </summary>
    Task<bool> IsInsideAreaAsync(int areaId, decimal lat, decimal lng, CancellationToken cancellationToken = default);

    /// <summary>
    /// 학원가의 살아 있는 제외·단속 구역 수를 조회한다
    /// </summary>
    Task<int> CountRestrictedZonesAsync(int areaId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 좌표가 걸리는 제외·단속 구역 유형을 중복 없이 조회한다.
    /// 요일 유형과 시각(at)에 적용되는 구역만 본다
    /// </summary>
    Task<List<string>> GetMatchedZoneTypesAsync(int areaId, decimal lat, decimal lng, string dayType, TimeSpan at,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 로그인한 부모가 살아 있는 시간표로 이용하는 학원가를 조회한다
    /// </summary>
    Task<List<UserAreaRow>> GetUserAreasAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 학원가들의 단속 구역 목록 버전 계산 값을 조회한다
    /// </summary>
    Task<ZoneVersionRow> GetRestrictedZoneVersionAsync(int[] areaIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 학원가들의 살아 있는 단속 구역을 조회한다
    /// </summary>
    Task<List<ZoneRow>> GetRestrictedZonesAsync(int[] areaIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// 오늘 계획 행을 FOR UPDATE로 잠그고 읽는다. 트랜잭션의 첫 문장으로 부른다.
    /// DELETE_YN 조건을 붙이지 않으므로 끝난 계획도 돌려준다(USER_ID가 NULL)
    /// </summary>
    Task<LinkPlanLockRow?> LockPickupPlanAsync(int pickupPlanId, IDbTransaction transaction,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 링크 문자의 운전자 이름, 아이 번호(암호문), 지점 이름을 조회한다. 살아 있는 계정이 없으면 null.
    /// 지점이 null(임의 좌표 계획)이면 SpotName은 null이다
    /// </summary>
    Task<LinkTargetRow?> GetLinkTargetAsync(int userId, int childId, int? pickupSpotId, IDbTransaction transaction,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 오늘 계획의 링크 관련 이벤트(LINK_SENT·REMIND_SENT·LINK_OPENED·CHILD_LEAVING)를 PICKUP_EVENT_ID 순으로 조회한다
    /// </summary>
    Task<List<LinkEventRow>> GetLinkEventsAsync(int pickupPlanId, IDbTransaction? transaction,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 로그인한 부모의 오늘 계획 상태와 만료 시각을 잠금 없이 조회한다. 계획이 없거나 남의 계획이거나 끝난 계획(USER_ID가 NULL)이면 null
    /// </summary>
    Task<LinkStatusPlanRow?> GetLinkStatusPlanAsync(int pickupPlanId, int userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 로그인한 부모의 진행 중 계획(그날 PICKUP_DATE, 끝나지 않은 STATUS, EXPIRE_DT > now) 중
    /// 배정 시각(없으면 그날 하원 시각)이 가장 이른 하나를 잠금 없이 조회한다. 없으면 null
    /// </summary>
    Task<LivePlanRow?> GetLivePlanAsync(int userId, DateOnly date, DateTime now,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 오늘 계획 한 건을 상태와 상관없이 조회한다(응답 만들기용). 없으면 null
    /// </summary>
    Task<LivePlanRow?> GetPlanByIdAsync(int pickupPlanId, IDbTransaction? transaction,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 시간표 요일·날짜로 오늘 계획 ID를 조회한다. UNIQUE(TIMETABLE_DAY_ID, PICKUP_DATE)와 같은 키라 상태 조건을 붙이지 않는다
    /// (끝난 계획은 TIMETABLE_DAY_ID가 NULL이라 걸리지 않는다). 없으면 null
    /// </summary>
    Task<int?> GetLivePlanIdByTimetableDayAsync(int timetableDayId, DateOnly date, IDbTransaction? transaction,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 학원가 행을 FOR UPDATE로 잠근다. 배정·재배정 트랜잭션의 첫 문장으로 부른다. 행이 없으면 false
    /// </summary>
    Task<bool> LockAreaAsync(int areaId, IDbTransaction transaction, CancellationToken cancellationToken = default);

    /// <summary>
    /// 시간표 요일 행을 FOR UPDATE로 잠그고 시간표·아이·학원·그날 예외를 함께 읽는다. 잠그는 행은 TIMETABLE_DAY 하나다.
    /// DELETE_YN 조건을 붙이지 않고 Alive로 돌려준다. 행이 없으면 null
    /// </summary>
    Task<TimetableDayLockRow?> LockTimetableDayAsync(int timetableDayId, DateOnly date, IDbTransaction transaction,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 지점에서 배정 시각 앞뒤 windowMin분 안에 있는 진행 중 계획(끝나지 않은 STATUS, EXPIRE_DT > now) 수를 센다.
    /// excludePlanId가 있으면 그 계획은 빼고 센다
    /// </summary>
    Task<int> CountSlotPlansAsync(int pickupSpotId, DateTime slotDt, int windowMin, DateTime now, int? excludePlanId,
        IDbTransaction transaction, CancellationToken cancellationToken = default);

    /// <summary>
    /// 오늘 계획 행을 PK 인덱스(FORCE INDEX (PRIMARY))로 FOR UPDATE 잠그고 읽는다. 재배정 트랜잭션에서 AREA 잠금 다음에 부른다.
    /// DELETE_YN 조건을 붙이지 않으므로 끝난 계획도 돌려준다(UserId가 NULL). 행이 없으면 null
    /// </summary>
    Task<PlanLockRow?> LockPickupPlanByIdAsync(int pickupPlanId, IDbTransaction transaction,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 오늘 계획의 임의 좌표 도보 경로 선(CUSTOM_WALK_PATH)을 바꾼다.
    /// lineStringWkt는 SRID 4326 WKT("LINESTRING(위도 경도,…)")이고 null이면 비운다
    /// </summary>
    Task SetCustomWalkPathAsync(int pickupPlanId, string? lineStringWkt, IDbTransaction transaction,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 오늘 계획에서 그 차량 이벤트 번호(CLIENT_EVENT_ID)로 이미 들어간 이벤트의 유형을 조회한다. 없으면 null
    /// </summary>
    Task<string?> GetPlanEventTypeAsync(int pickupPlanId, Guid clientEventId, IDbTransaction transaction,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 픽업 지점의 정원(CAPACITY)을 조회한다. 지점이 없거나 삭제됐으면 null
    /// </summary>
    Task<byte?> GetPickupSpotCapacityAsync(int pickupSpotId, IDbTransaction transaction,
        CancellationToken cancellationToken = default);
}
