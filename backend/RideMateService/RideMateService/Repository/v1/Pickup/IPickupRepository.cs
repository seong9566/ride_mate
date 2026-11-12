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
    /// 조회 시각이 저장된 시각보다 나중일 때 주차장 빈자리 캐시를 갱신한다
    /// </summary>
    Task<int> UpdateAvailableSpacesAsync(int parkingLotId, short availableSpaces, DateTime checkedDt,
        CancellationToken cancellationToken = default);

    // ====== Dapper

    /// <summary>
    /// 로그인한 부모의 살아 있는 아이 시간표 중 그날 요일에 픽업하는 것을 조회한다.
    /// 그날 SKIP 예외는 빼고, TIME_CHANGE 예외는 바뀐 하원 시각을 EndTime으로 돌려준다. 그날 이미 픽업을 마친 것은 뺀다
    /// </summary>
    Task<List<TodayPickupRow>> GetTodayPickupsAsync(int userId, DateOnly date, byte dayOfWeek,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 학원가의 살아 있는 대기 장소를 조회한다
    /// </summary>
    Task<List<ParkingLotRow>> GetParkingLotsAsync(int areaId, CancellationToken cancellationToken = default);

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
    /// 링크 문자의 운전자 이름, 아이 번호(암호문), 지점 이름을 조회한다. 살아 있는 계정이 없으면 null
    /// </summary>
    Task<LinkTargetRow?> GetLinkTargetAsync(int userId, int childId, int pickupSpotId, IDbTransaction transaction,
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
    /// 토큰 해시로 픽업 세션의 계획 ID와 종료 시각을 잠금 없이 조회한다. 세션이 없으면 null
    /// </summary>
    Task<LeaveSessionRow?> GetLeaveSessionAsync(string tokenHash, CancellationToken cancellationToken = default);

    /// <summary>
    /// 아이 출발 관련 이벤트(CHILD_LEAVING·LEAVE_SCHEDULED·LEAVE_CANCELED)를 PICKUP_EVENT_ID 순으로 조회한다
    /// </summary>
    Task<List<LinkEventRow>> GetLeaveEventsAsync(int pickupPlanId, IDbTransaction? transaction,
        CancellationToken cancellationToken = default);
}
