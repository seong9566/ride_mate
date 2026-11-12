using System.Data;
using Dapper;

namespace RideMateService.Repository.v1.Pickup;

public partial class PickupRepository
{
    /// <summary>
    /// 로그인한 부모의 살아 있는 아이 시간표 중 그날 요일에 픽업하는 것을 조회한다.
    /// 그날 SKIP 예외는 빼고, TIME_CHANGE 예외는 바뀐 하원 시각을 EndTime으로 돌려준다. 그날 이미 픽업을 마친 것은 뺀다
    /// </summary>
    public async Task<List<TodayPickupRow>> GetTodayPickupsAsync(int userId, DateOnly date, byte dayOfWeek,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var dp = new DynamicParameters();
            dp.Add("userId", userId);
            // DateOnly를 그대로 넘기면 드라이버가 문화권 형식 문자열('11/11/2026')로 보내 DATE 비교가 실패한다
            dp.Add("date", date.ToDateTime(TimeOnly.MinValue), DbType.Date);
            dp.Add("dayOfWeek", dayOfWeek);

            var sql = @"
                SELECT td.TIMETABLE_DAY_ID AS TimetableDayId,
                       a.ACADEMY_ID AS AcademyId,
                       a.AREA_ID AS AreaId,
                       t.DISPLAY_NAME AS DisplayName,
                       CASE WHEN e.EXCEPTION_TYPE = 'TIME_CHANGE' THEN e.CHANGED_END_TIME ELSE t.END_TIME END AS EndTime,
                       a.LAT AS AcademyLat,
                       a.LNG AS AcademyLng
                FROM CHILD AS c
                INNER JOIN TIMETABLE AS t
                        ON t.CHILD_ID = c.CHILD_ID
                       AND t.DELETE_YN = FALSE
                INNER JOIN TIMETABLE_DAY AS td
                        ON td.TIMETABLE_ID = t.TIMETABLE_ID
                       AND td.DAY_OF_WEEK = @dayOfWeek
                       AND td.PICKUP_YN = 1
                       AND (td.LAST_PICKUP_DATE IS NULL OR td.LAST_PICKUP_DATE <> @date)
                       AND td.DELETE_YN = FALSE
                INNER JOIN ACADEMY AS a
                        ON a.ACADEMY_ID = t.ACADEMY_ID
                       AND a.DELETE_YN = FALSE
                LEFT JOIN TIMETABLE_EXCEPTION AS e
                       ON e.TIMETABLE_DAY_ID = td.TIMETABLE_DAY_ID
                      AND e.TARGET_DATE = @date
                      AND e.DELETE_YN = FALSE
                WHERE c.USER_ID = @userId
                  AND c.DELETE_YN = FALSE
                  AND (e.EXCEPTION_TYPE IS NULL OR e.EXCEPTION_TYPE <> 'SKIP')";

            var rows = await _dapper.QueryAsync<TodayPickupRow>(
                new CommandDefinition(sql, dp, cancellationToken: cancellationToken)).ConfigureAwait(false);
            return rows.ToList();
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested) // 요청 취소는 로그 없이 올려보낸다
        {
            _logService.LogMessage(ex.ToString());
#if DEBUG
            _logService.ConsoleWarning(ex.ToString());
#endif
            throw;
        }
    }

    /// <summary>
    /// 학원가의 살아 있는 대기 장소를 조회한다
    /// </summary>
    public async Task<List<ParkingLotRow>> GetParkingLotsAsync(int areaId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var dp = new DynamicParameters();
            dp.Add("areaId", areaId);

            var sql = @"
                SELECT p.PARKING_LOT_ID AS ParkingLotId,
                       p.LOT_NAME AS LotName,
                       p.LAT AS Lat,
                       p.LNG AS Lng,
                       p.WEEKDAY_OPEN_TIME AS WeekdayOpenTime,
                       p.WEEKDAY_CLOSE_TIME AS WeekdayCloseTime,
                       p.SAT_OPEN_TIME AS SatOpenTime,
                       p.SAT_CLOSE_TIME AS SatCloseTime,
                       p.HOLIDAY_OPEN_TIME AS HolidayOpenTime,
                       p.HOLIDAY_CLOSE_TIME AS HolidayCloseTime,
                       p.BASE_MIN AS BaseMin,
                       p.BASE_FEE AS BaseFee,
                       p.ADD_UNIT_MIN AS AddUnitMin,
                       p.ADD_FEE AS AddFee,
                       p.DAY_PASS_FEE AS DayPassFee,
                       p.DAY_MAX_FEE AS DayMaxFee,
                       p.REALTIME_REF AS RealtimeRef,
                       p.AVAILABLE_SPACES AS AvailableSpaces,
                       p.AVAILABLE_UPDATE_DT AS AvailableUpdateDt
                FROM PARKING_LOT AS p
                WHERE p.AREA_ID = @areaId
                  AND p.DELETE_YN = FALSE
                ORDER BY p.PARKING_LOT_ID";

            var rows = await _dapper.QueryAsync<ParkingLotRow>(
                new CommandDefinition(sql, dp, cancellationToken: cancellationToken)).ConfigureAwait(false);
            return rows.ToList();
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested) // 요청 취소는 로그 없이 올려보낸다
        {
            _logService.LogMessage(ex.ToString());
#if DEBUG
            _logService.ConsoleWarning(ex.ToString());
#endif
            throw;
        }
    }

    /// <summary>
    /// 학원의 픽업 지점 후보 중 사용 중인 것을 우선순위 순으로 조회한다
    /// </summary>
    public async Task<List<PickupSpotRow>> GetAcademyPickupSpotsAsync(int academyId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var dp = new DynamicParameters();
            dp.Add("academyId", academyId);

            var sql = @"
                SELECT s.PICKUP_SPOT_ID AS PickupSpotId,
                       s.SPOT_NAME AS SpotName,
                       s.LAT AS Lat,
                       s.LNG AS Lng,
                       aps.WALK_MIN AS WalkMin,
                       ST_AsText(aps.WALK_PATH) AS WalkPathWkt
                FROM ACADEMY_PICKUP_SPOT AS aps
                INNER JOIN PICKUP_SPOT AS s
                        ON s.PICKUP_SPOT_ID = aps.PICKUP_SPOT_ID
                       AND s.USE_YN = 1
                       AND s.DELETE_YN = FALSE
                WHERE aps.ACADEMY_ID = @academyId
                  AND aps.DELETE_YN = FALSE
                ORDER BY aps.PRIORITY, aps.ACADEMY_PICKUP_SPOT_ID";

            var rows = await _dapper.QueryAsync<PickupSpotRow>(
                new CommandDefinition(sql, dp, cancellationToken: cancellationToken)).ConfigureAwait(false);
            return rows.ToList();
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested) // 요청 취소는 로그 없이 올려보낸다
        {
            _logService.LogMessage(ex.ToString());
#if DEBUG
            _logService.ConsoleWarning(ex.ToString());
#endif
            throw;
        }
    }

    /// <summary>
    /// 픽업 지점들의 그 요일 유형 금지 시간을 조회한다
    /// </summary>
    public async Task<List<SpotBlockRow>> GetPickupSpotBlocksAsync(int[] pickupSpotIds, string dayType,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var dp = new DynamicParameters();
            dp.Add("pickupSpotIds", pickupSpotIds);
            dp.Add("dayType", dayType);

            var sql = @"
                SELECT b.PICKUP_SPOT_ID AS PickupSpotId,
                       b.START_TIME AS StartTime,
                       b.END_TIME AS EndTime,
                       b.REASON AS Reason
                FROM PICKUP_SPOT_BLOCK AS b
                WHERE b.PICKUP_SPOT_ID IN @pickupSpotIds
                  AND b.DAY_TYPE = @dayType
                  AND b.DELETE_YN = FALSE
                ORDER BY b.PICKUP_SPOT_ID, b.START_TIME";

            var rows = await _dapper.QueryAsync<SpotBlockRow>(
                new CommandDefinition(sql, dp, cancellationToken: cancellationToken)).ConfigureAwait(false);
            return rows.ToList();
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested) // 요청 취소는 로그 없이 올려보낸다
        {
            _logService.LogMessage(ex.ToString());
#if DEBUG
            _logService.ConsoleWarning(ex.ToString());
#endif
            throw;
        }
    }

    /// <summary>
    /// 그날이 속한 해의 공휴일 적재 여부와 그날의 공휴일 여부를 조회한다
    /// </summary>
    public async Task<HolidayRow> GetHolidayAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        try
        {
            var dp = new DynamicParameters();
            dp.Add("date", date.ToDateTime(TimeOnly.MinValue), DbType.Date);
            dp.Add("yearStart", new DateTime(date.Year, 1, 1), DbType.Date);
            dp.Add("nextYearStart", new DateTime(date.Year + 1, 1, 1), DbType.Date);

            var sql = @"
                SELECT EXISTS (SELECT 1
                               FROM HOLIDAY AS h
                               WHERE h.HOLIDAY_DATE >= @yearStart
                                 AND h.HOLIDAY_DATE < @nextYearStart
                                 AND h.DELETE_YN = FALSE) AS YearLoaded,
                       EXISTS (SELECT 1
                               FROM HOLIDAY AS h
                               WHERE h.HOLIDAY_DATE = @date
                                 AND h.DELETE_YN = FALSE) AS IsHoliday";

            return await _dapper.QuerySingleAsync<HolidayRow>(
                new CommandDefinition(sql, dp, cancellationToken: cancellationToken)).ConfigureAwait(false);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested) // 요청 취소는 로그 없이 올려보낸다
        {
            _logService.LogMessage(ex.ToString());
#if DEBUG
            _logService.ConsoleWarning(ex.ToString());
#endif
            throw;
        }
    }

    /// <summary>
    /// 좌표가 학원가 경계 안인지 조회한다. 학원가가 없으면 false
    /// </summary>
    public async Task<bool> IsInsideAreaAsync(int areaId, decimal lat, decimal lng,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var dp = new DynamicParameters();
            dp.Add("areaId", areaId);
            dp.Add("lat", lat);
            dp.Add("lng", lng);

            // SRID 4326 WKT는 위도 경도 순이다
            var sql = @"
                SELECT ST_Contains(a.BOUNDARY, ST_GeomFromText(CONCAT('POINT(', @lat, ' ', @lng, ')'), 4326))
                FROM AREA AS a
                WHERE a.AREA_ID = @areaId
                  AND a.DELETE_YN = FALSE";

            var inside = await _dapper.QueryFirstOrDefaultAsync<int?>(
                new CommandDefinition(sql, dp, cancellationToken: cancellationToken)).ConfigureAwait(false);
            return inside == 1;
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested) // 요청 취소는 로그 없이 올려보낸다
        {
            _logService.LogMessage(ex.ToString());
#if DEBUG
            _logService.ConsoleWarning(ex.ToString());
#endif
            throw;
        }
    }

    /// <summary>
    /// 학원가의 살아 있는 제외·단속 구역 수를 조회한다
    /// </summary>
    public async Task<int> CountRestrictedZonesAsync(int areaId, CancellationToken cancellationToken = default)
    {
        try
        {
            var dp = new DynamicParameters();
            dp.Add("areaId", areaId);

            var sql = @"
                SELECT COUNT(*)
                FROM RESTRICTED_ZONE AS z
                WHERE z.AREA_ID = @areaId
                  AND z.DELETE_YN = FALSE";

            return await _dapper.ExecuteScalarAsync<int>(
                new CommandDefinition(sql, dp, cancellationToken: cancellationToken)).ConfigureAwait(false);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested) // 요청 취소는 로그 없이 올려보낸다
        {
            _logService.LogMessage(ex.ToString());
#if DEBUG
            _logService.ConsoleWarning(ex.ToString());
#endif
            throw;
        }
    }

    /// <summary>
    /// 좌표가 걸리는 제외·단속 구역 유형을 중복 없이 조회한다.
    /// 요일 유형과 시각(at)에 적용되는 구역만 본다
    /// </summary>
    public async Task<List<string>> GetMatchedZoneTypesAsync(int areaId, decimal lat, decimal lng, string dayType,
        TimeSpan at, CancellationToken cancellationToken = default)
    {
        try
        {
            var dp = new DynamicParameters();
            dp.Add("areaId", areaId);

            // 1) 후보 사각형 반경 — 그 학원가에서 가장 큰 BUFFER_M(m)
            var maxBufferSql = @"
                SELECT COALESCE(MAX(z.BUFFER_M), 0)
                FROM RESTRICTED_ZONE AS z
                WHERE z.AREA_ID = @areaId
                  AND z.DELETE_YN = FALSE";

            var maxBufferM = await _dapper.ExecuteScalarAsync<int>(
                new CommandDefinition(maxBufferSql, dp, cancellationToken: cancellationToken)).ConfigureAwait(false);

            // m를 도 단위로 바꾼다. 위도 1도 = 111,320m, 경도 1도 = 111,320m × cos(위도)
            var deltaLat = maxBufferM / 111320.0;
            var deltaLng = maxBufferM / (111320.0 * Math.Cos(double.DegreesToRadians((double)lat)));

            dp.Add("lat", lat);
            dp.Add("lng", lng);
            dp.Add("minLat", Math.Round(lat - (decimal)deltaLat, 7));
            dp.Add("maxLat", Math.Round(lat + (decimal)deltaLat, 7));
            dp.Add("minLng", Math.Round(lng - (decimal)deltaLng, 7));
            dp.Add("maxLng", Math.Round(lng + (decimal)deltaLng, 7));
            dp.Add("dayType", dayType);
            dp.Add("at", at);

            // 2) 사각형과 MBRIntersects로 후보를 추린 뒤(공간 인덱스) ST_Distance(m)로 거른다.
            //    MySQL 8.4 SRID 4326에서 점↔점·선·면 ST_Distance가 m로 나오는 것을 확인했다(면 안쪽은 0)
            var sql = @"
                SELECT DISTINCT z.ZONE_TYPE
                FROM RESTRICTED_ZONE AS z
                WHERE z.AREA_ID = @areaId
                  AND z.DELETE_YN = FALSE
                  AND MBRIntersects(z.GEOM, ST_GeomFromText(CONCAT('POLYGON((',
                        @minLat, ' ', @minLng, ', ',
                        @maxLat, ' ', @minLng, ', ',
                        @maxLat, ' ', @maxLng, ', ',
                        @minLat, ' ', @maxLng, ', ',
                        @minLat, ' ', @minLng, '))'), 4326))
                  AND ST_Distance(z.GEOM, ST_GeomFromText(CONCAT('POINT(', @lat, ' ', @lng, ')'), 4326)) <= z.BUFFER_M
                  AND (z.DAY_TYPE IS NULL OR z.DAY_TYPE = @dayType)
                  AND (z.ACTIVE_START_TIME IS NULL OR @at BETWEEN z.ACTIVE_START_TIME AND z.ACTIVE_END_TIME)
                ORDER BY z.ZONE_TYPE";

            var rows = await _dapper.QueryAsync<string>(
                new CommandDefinition(sql, dp, cancellationToken: cancellationToken)).ConfigureAwait(false);
            return rows.ToList();
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested) // 요청 취소는 로그 없이 올려보낸다
        {
            _logService.LogMessage(ex.ToString());
#if DEBUG
            _logService.ConsoleWarning(ex.ToString());
#endif
            throw;
        }
    }

    /// <summary>
    /// 로그인한 부모가 살아 있는 시간표로 이용하는 학원가를 조회한다
    /// </summary>
    public async Task<List<UserAreaRow>> GetUserAreasAsync(int userId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var dp = new DynamicParameters();
            dp.Add("userId", userId);

            var sql = @"
                SELECT DISTINCT ar.AREA_ID AS AreaId,
                                ar.AREA_NAME AS AreaName,
                                COALESCE(ar.UPDATE_DT, ar.CREATE_DT) AS AreaUpdatedDt
                FROM CHILD AS c
                INNER JOIN TIMETABLE AS t
                        ON t.CHILD_ID = c.CHILD_ID
                       AND t.DELETE_YN = FALSE
                INNER JOIN ACADEMY AS a
                        ON a.ACADEMY_ID = t.ACADEMY_ID
                       AND a.DELETE_YN = FALSE
                INNER JOIN AREA AS ar
                        ON ar.AREA_ID = a.AREA_ID
                       AND ar.DELETE_YN = FALSE
                WHERE c.USER_ID = @userId
                  AND c.DELETE_YN = FALSE
                ORDER BY ar.AREA_ID";

            var rows = await _dapper.QueryAsync<UserAreaRow>(
                new CommandDefinition(sql, dp, cancellationToken: cancellationToken)).ConfigureAwait(false);
            return rows.ToList();
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested) // 요청 취소는 로그 없이 올려보낸다
        {
            _logService.LogMessage(ex.ToString());
#if DEBUG
            _logService.ConsoleWarning(ex.ToString());
#endif
            throw;
        }
    }

    /// <summary>
    /// 학원가들의 단속 구역 목록 버전 계산 값을 조회한다
    /// </summary>
    public async Task<ZoneVersionRow> GetRestrictedZoneVersionAsync(int[] areaIds,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var dp = new DynamicParameters();
            dp.Add("areaIds", areaIds);

            var sql = @"
                SELECT MAX(COALESCE(z.UPDATE_DT, z.CREATE_DT)) AS MaxDt,
                       COALESCE(SUM(CASE WHEN z.DELETE_YN = FALSE THEN 1 ELSE 0 END), 0) AS LiveCount
                FROM RESTRICTED_ZONE AS z
                WHERE z.AREA_ID IN @areaIds";

            return await _dapper.QuerySingleAsync<ZoneVersionRow>(
                new CommandDefinition(sql, dp, cancellationToken: cancellationToken)).ConfigureAwait(false);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested) // 요청 취소는 로그 없이 올려보낸다
        {
            _logService.LogMessage(ex.ToString());
#if DEBUG
            _logService.ConsoleWarning(ex.ToString());
#endif
            throw;
        }
    }

    /// <summary>
    /// 학원가들의 살아 있는 단속 구역을 조회한다
    /// </summary>
    public async Task<List<ZoneRow>> GetRestrictedZonesAsync(int[] areaIds,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var dp = new DynamicParameters();
            dp.Add("areaIds", areaIds);

            var sql = @"
                SELECT z.RESTRICTED_ZONE_ID AS RestrictedZoneId,
                       z.AREA_ID AS AreaId,
                       z.ZONE_TYPE AS ZoneType,
                       ST_AsText(z.GEOM, 'axis-order=long-lat') AS GeomWkt,
                       z.BUFFER_M AS BufferM,
                       z.DAY_TYPE AS DayType,
                       z.ACTIVE_START_TIME AS ActiveStartTime,
                       z.ACTIVE_END_TIME AS ActiveEndTime
                FROM RESTRICTED_ZONE AS z
                WHERE z.AREA_ID IN @areaIds
                  AND z.DELETE_YN = FALSE
                ORDER BY z.AREA_ID, z.RESTRICTED_ZONE_ID";

            var rows = await _dapper.QueryAsync<ZoneRow>(
                new CommandDefinition(sql, dp, cancellationToken: cancellationToken)).ConfigureAwait(false);
            return rows.ToList();
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested) // 요청 취소는 로그 없이 올려보낸다
        {
            _logService.LogMessage(ex.ToString());
#if DEBUG
            _logService.ConsoleWarning(ex.ToString());
#endif
            throw;
        }
    }

    /// <summary>
    /// 오늘 계획 행을 FOR UPDATE로 잠그고 읽는다. 트랜잭션의 첫 문장으로 부른다.
    /// DELETE_YN 조건을 붙이지 않으므로 끝난 계획도 돌려준다(USER_ID가 NULL)
    /// </summary>
    public async Task<LinkPlanLockRow?> LockPickupPlanAsync(int pickupPlanId, IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var dp = new DynamicParameters();
            dp.Add("pickupPlanId", pickupPlanId);

            var sql = @"
                SELECT p.PICKUP_PLAN_ID AS PickupPlanId,
                       p.USER_ID AS UserId,
                       p.CHILD_ID AS ChildId,
                       p.PICKUP_SPOT_ID AS PickupSpotId,
                       p.STATUS AS Status,
                       p.EXPIRE_DT AS ExpireDt
                FROM PICKUP_PLAN AS p
                WHERE p.PICKUP_PLAN_ID = @pickupPlanId
                FOR UPDATE";

            return await _dapper.QueryFirstOrDefaultAsync<LinkPlanLockRow>(
                new CommandDefinition(sql, dp, transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested) // 요청 취소는 로그 없이 올려보낸다
        {
            _logService.LogMessage(ex.ToString());
#if DEBUG
            _logService.ConsoleWarning(ex.ToString());
#endif
            throw;
        }
    }

    /// <summary>
    /// 링크 문자의 운전자 이름, 아이 번호(암호문), 지점 이름을 조회한다. 살아 있는 계정이 없으면 null
    /// </summary>
    public async Task<LinkTargetRow?> GetLinkTargetAsync(int userId, int childId, int pickupSpotId,
        IDbTransaction transaction, CancellationToken cancellationToken = default)
    {
        try
        {
            var dp = new DynamicParameters();
            dp.Add("userId", userId);
            dp.Add("childId", childId);
            dp.Add("pickupSpotId", pickupSpotId);

            var sql = @"
                SELECT u.USER_NAME AS UserName,
                       c.PHONE_ENC AS ChildPhoneEnc,
                       s.SPOT_NAME AS SpotName
                FROM USER_INFO AS u
                LEFT JOIN CHILD AS c
                       ON c.CHILD_ID = @childId
                      AND c.USER_ID = u.USER_ID
                      AND c.DELETE_YN = FALSE
                LEFT JOIN PICKUP_SPOT AS s
                       ON s.PICKUP_SPOT_ID = @pickupSpotId
                      AND s.DELETE_YN = FALSE
                WHERE u.USER_ID = @userId
                  AND u.DELETE_YN = FALSE";

            return await _dapper.QueryFirstOrDefaultAsync<LinkTargetRow>(
                new CommandDefinition(sql, dp, transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested) // 요청 취소는 로그 없이 올려보낸다
        {
            _logService.LogMessage(ex.ToString());
#if DEBUG
            _logService.ConsoleWarning(ex.ToString());
#endif
            throw;
        }
    }

    /// <summary>
    /// 오늘 계획의 링크 관련 이벤트(LINK_SENT·REMIND_SENT·LINK_OPENED·CHILD_LEAVING)를 PICKUP_EVENT_ID 순으로 조회한다
    /// </summary>
    public async Task<List<LinkEventRow>> GetLinkEventsAsync(int pickupPlanId, IDbTransaction? transaction,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var dp = new DynamicParameters();
            dp.Add("pickupPlanId", pickupPlanId);

            // 픽업 계열 테이블은 DELETE_YN이 "끝나서 익명화됨"이라 조건을 붙이지 않는다(schema.md).
            // 드라이버가 CHAR(36)을 Guid로 읽어 string에 매핑하지 못하므로 CLIENT_EVENT_ID는 문자열로 바꿔 읽는다
            var sql = @"
                SELECT e.PICKUP_EVENT_ID AS PickupEventId,
                       e.EVENT_TYPE AS EventType,
                       e.EVENT_DT AS EventDt,
                       CAST(e.CLIENT_EVENT_ID AS CHAR) AS ClientEventId,
                       e.DETAIL AS Detail,
                       e.CREATE_DT AS CreateDt
                FROM PICKUP_EVENT AS e
                WHERE e.PICKUP_PLAN_ID = @pickupPlanId
                  AND e.EVENT_TYPE IN ('LINK_SENT', 'REMIND_SENT', 'LINK_OPENED', 'CHILD_LEAVING')
                ORDER BY e.PICKUP_EVENT_ID";

            var rows = await _dapper.QueryAsync<LinkEventRow>(
                new CommandDefinition(sql, dp, transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
            return rows.ToList();
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested) // 요청 취소는 로그 없이 올려보낸다
        {
            _logService.LogMessage(ex.ToString());
#if DEBUG
            _logService.ConsoleWarning(ex.ToString());
#endif
            throw;
        }
    }

    /// <summary>
    /// 로그인한 부모의 오늘 계획 상태와 만료 시각을 잠금 없이 조회한다. 계획이 없거나 남의 계획이거나 끝난 계획(USER_ID가 NULL)이면 null
    /// </summary>
    public async Task<LinkStatusPlanRow?> GetLinkStatusPlanAsync(int pickupPlanId, int userId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var dp = new DynamicParameters();
            dp.Add("pickupPlanId", pickupPlanId);
            dp.Add("userId", userId);

            // 픽업 계열 테이블은 DELETE_YN이 "끝나서 익명화됨"이라 조건을 붙이지 않는다. 끝난 계획은 USER_ID가 NULL이라 걸러진다
            var sql = @"
                SELECT p.STATUS AS Status,
                       p.EXPIRE_DT AS ExpireDt
                FROM PICKUP_PLAN AS p
                WHERE p.PICKUP_PLAN_ID = @pickupPlanId
                  AND p.USER_ID = @userId";

            return await _dapper.QueryFirstOrDefaultAsync<LinkStatusPlanRow>(
                new CommandDefinition(sql, dp, cancellationToken: cancellationToken)).ConfigureAwait(false);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested) // 요청 취소는 로그 없이 올려보낸다
        {
            _logService.LogMessage(ex.ToString());
#if DEBUG
            _logService.ConsoleWarning(ex.ToString());
#endif
            throw;
        }
    }
}
