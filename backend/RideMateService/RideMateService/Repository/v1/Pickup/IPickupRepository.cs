namespace RideMateService.Repository.v1.Pickup;

public interface IPickupRepository
{
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
}
