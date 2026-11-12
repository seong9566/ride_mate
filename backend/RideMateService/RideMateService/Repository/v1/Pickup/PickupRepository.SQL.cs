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
                       a.LNG AS AcademyLng,
                       t.PICKUP_SPOT_ID AS PreferredSpotId
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
    /// 학원가의 요일·5분 칸별 대표 속도를 조회한다. 학원가 코드가 없으면 빈 목록을 돌려준다
    /// </summary>
    public async Task<List<TrafficProfileRow>> GetTrafficProfileAsync(int areaId, byte dayOfWeek, short[] slotMins,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var dp = new DynamicParameters();
            dp.Add("areaId", areaId);
            dp.Add("dayOfWeek", dayOfWeek);
            dp.Add("slotMins", slotMins);

            var sql = @"
                SELECT p.AREA_CODE AS AreaCode,
                       p.SOURCE AS Source,
                       p.SLOT_MIN AS SlotMin,
                       p.SPEED_KMH AS SpeedKmh,
                       p.SAMPLE_DAYS AS SampleDays
                FROM AREA AS a
                INNER JOIN TRAFFIC_PROFILE AS p
                        ON p.AREA_CODE = a.AREA_CODE
                WHERE a.AREA_ID = @areaId
                  AND a.DELETE_YN = 0
                  AND p.DELETE_YN = 0
                  AND p.DAY_OF_WEEK = @dayOfWeek
                  AND p.SLOT_MIN IN @slotMins";

            var rows = await _dapper.QueryAsync<TrafficProfileRow>(
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
    /// 주차장이 그 학원가(AREA_ID)의 살아 있는 대기 장소인지 본다. 없거나 삭제됐거나 다른 학원가면 false
    /// </summary>
    public async Task<bool> IsParkingLotInAreaAsync(int parkingLotId, int areaId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var dp = new DynamicParameters();
            dp.Add("parkingLotId", parkingLotId);
            dp.Add("areaId", areaId);

            var sql = @"
                SELECT EXISTS (
                    SELECT 1
                    FROM PARKING_LOT AS p
                    WHERE p.PARKING_LOT_ID = @parkingLotId
                      AND p.AREA_ID = @areaId
                      AND p.DELETE_YN = FALSE)";

            return await _dapper.ExecuteScalarAsync<bool>(
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
    /// 로그인한 부모의 살아 있는 아이의 살아 있는 시간표 한 건을 픽업 장소 후보 목록용으로 조회한다. 없거나 남의 것이면 null.
    /// 그 날짜 요일의 살아 있는 요일 행이 있으면 TIME_CHANGE 예외를 반영한 하원 시각을 EndTime으로 돌려준다
    /// </summary>
    public async Task<PickupSpotTargetRow?> GetPickupSpotTargetAsync(int userId, int timetableId, DateOnly date,
        byte dayOfWeek, CancellationToken cancellationToken = default)
    {
        try
        {
            var dp = new DynamicParameters();
            dp.Add("userId", userId);
            dp.Add("timetableId", timetableId);
            dp.Add("date", date.ToDateTime(TimeOnly.MinValue), DbType.Date);
            dp.Add("dayOfWeek", dayOfWeek);

            var sql = @"
                SELECT t.TIMETABLE_ID AS TimetableId,
                       t.DISPLAY_NAME AS DisplayName,
                       a.ACADEMY_ID AS AcademyId,
                       a.AREA_ID AS AreaId,
                       ar.AREA_NAME AS AreaName,
                       a.LAT AS AcademyLat,
                       a.LNG AS AcademyLng,
                       t.PICKUP_SPOT_ID AS PreferredSpotId,
                       CASE WHEN e.EXCEPTION_TYPE = 'TIME_CHANGE' THEN e.CHANGED_END_TIME ELSE t.END_TIME END AS EndTime,
                       td.TIMETABLE_DAY_ID AS TimetableDayId
                FROM TIMETABLE AS t
                INNER JOIN CHILD AS c
                        ON c.CHILD_ID = t.CHILD_ID
                       AND c.DELETE_YN = FALSE
                INNER JOIN ACADEMY AS a
                        ON a.ACADEMY_ID = t.ACADEMY_ID
                       AND a.DELETE_YN = FALSE
                LEFT JOIN AREA AS ar
                       ON ar.AREA_ID = a.AREA_ID
                      AND ar.DELETE_YN = FALSE
                LEFT JOIN TIMETABLE_DAY AS td
                       ON td.TIMETABLE_ID = t.TIMETABLE_ID
                      AND td.DAY_OF_WEEK = @dayOfWeek
                      AND td.DELETE_YN = FALSE
                LEFT JOIN TIMETABLE_EXCEPTION AS e
                       ON e.TIMETABLE_DAY_ID = td.TIMETABLE_DAY_ID
                      AND e.TARGET_DATE = @date
                      AND e.DELETE_YN = FALSE
                WHERE t.TIMETABLE_ID = @timetableId
                  AND t.DELETE_YN = FALSE
                  AND c.USER_ID = @userId
                LIMIT 1";

            return await _dapper.QueryFirstOrDefaultAsync<PickupSpotTargetRow>(
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
    /// 학원의 픽업 지점 후보 중 사용 중인 것을 조회한다. 우리 픽업 장소(preferredSpotId)가 후보면 맨 앞에, 나머지는 우선순위 순이다.
    /// 순서가 상관없으면 preferredSpotId에 null을 넘긴다
    /// </summary>
    public async Task<List<PickupSpotRow>> GetAcademyPickupSpotsAsync(int academyId, int? preferredSpotId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var dp = new DynamicParameters();
            dp.Add("academyId", academyId);
            dp.Add("preferredSpotId", preferredSpotId);

            var sql = @"
                SELECT s.PICKUP_SPOT_ID AS PickupSpotId,
                       s.SPOT_NAME AS SpotName,
                       s.LAT AS Lat,
                       s.LNG AS Lng,
                       aps.WALK_MIN AS WalkMin,
                       ST_AsText(aps.WALK_PATH) AS WalkPathWkt,
                       s.CAPACITY AS Capacity,
                       s.GUIDE_TEXT AS GuideText,
                       s.PHOTO_URL AS PhotoUrl,
                       aps.NOTE AS Note
                FROM ACADEMY_PICKUP_SPOT AS aps
                INNER JOIN PICKUP_SPOT AS s
                        ON s.PICKUP_SPOT_ID = aps.PICKUP_SPOT_ID
                       AND s.USE_YN = 1
                       AND s.DELETE_YN = FALSE
                WHERE aps.ACADEMY_ID = @academyId
                  AND aps.DELETE_YN = FALSE
                ORDER BY (s.PICKUP_SPOT_ID <=> @preferredSpotId) DESC, aps.PRIORITY, aps.ACADEMY_PICKUP_SPOT_ID";

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
                       p.CUSTOM_NAME AS CustomName,
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
    /// 링크 문자의 운전자 이름, 아이 번호(암호문), 지점 이름을 조회한다. 살아 있는 계정이 없으면 null.
    /// 지점이 null(임의 좌표 계획)이면 SpotName은 null이다
    /// </summary>
    public async Task<LinkTargetRow?> GetLinkTargetAsync(int userId, int childId, int? pickupSpotId,
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
    /// 오늘 계획의 링크 관련 이벤트(LINK_SENT·LINK_RESENT·REMIND_SENT·LINK_OPENED·CHILD_LEAVING)를 PICKUP_EVENT_ID 순으로 조회한다
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
                  AND e.EVENT_TYPE IN ('LINK_SENT', 'LINK_RESENT', 'REMIND_SENT', 'LINK_OPENED', 'CHILD_LEAVING')
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

    // 진행 중 계획 상태. 정원 세기와 진행 중 계획 판정이 같은 목록을 쓴다
    private const string LivePlanStatuses = "'PLANNED', 'STARTED', 'WAITING', 'STOPPED', 'LOOPING'";

    // LivePlanRow 조회의 SELECT·FROM 부분. 픽업 계열 테이블(PICKUP_PLAN)에는 DELETE_YN 조건을 붙이지 않는다(schema.md)
    private const string LivePlanSelectSql = @"
                SELECT p.PICKUP_PLAN_ID AS PickupPlanId,
                       p.USER_ID AS UserId,
                       p.TIMETABLE_DAY_ID AS TimetableDayId,
                       p.AREA_ID AS AreaId,
                       p.PICKUP_DATE AS PickupDate,
                       p.END_TIME AS EndTime,
                       p.STATUS AS Status,
                       p.PICKUP_SPOT_ID AS PickupSpotId,
                       p.SLOT_DT AS SlotDt,
                       p.WALK_MIN AS WalkMin,
                       p.EXPIRE_DT AS ExpireDt,
                       p.CUSTOM_LAT AS CustomLat,
                       p.CUSTOM_LNG AS CustomLng,
                       p.CUSTOM_NAME AS CustomName,
                       ST_AsText(p.CUSTOM_WALK_PATH) AS CustomWalkPathWkt,
                       s.SPOT_NAME AS SpotName,
                       s.LAT AS SpotLat,
                       s.LNG AS SpotLng,
                       s.GUIDE_TEXT AS GuideText,
                       s.PHOTO_URL AS PhotoUrl,
                       t.DISPLAY_NAME AS AcademyName,
                       p.ACADEMY_ID AS AcademyId,
                       a.LAT AS AcademyLat,
                       a.LNG AS AcademyLng,
                       p.PARKING_LOT_ID AS ParkingLotId,
                       t.PICKUP_SPOT_ID AS PreferredSpotId
                FROM PICKUP_PLAN AS p
                LEFT JOIN PICKUP_SPOT AS s
                       ON s.PICKUP_SPOT_ID = p.PICKUP_SPOT_ID
                      AND s.DELETE_YN = FALSE
                LEFT JOIN TIMETABLE_DAY AS td
                       ON td.TIMETABLE_DAY_ID = p.TIMETABLE_DAY_ID
                      AND td.DELETE_YN = FALSE
                LEFT JOIN TIMETABLE AS t
                       ON t.TIMETABLE_ID = td.TIMETABLE_ID
                      AND t.DELETE_YN = FALSE
                LEFT JOIN ACADEMY AS a
                       ON a.ACADEMY_ID = p.ACADEMY_ID";

    /// <summary>
    /// 로그인한 부모의 진행 중 계획(그날 PICKUP_DATE, 끝나지 않은 STATUS, EXPIRE_DT > now) 중
    /// 배정 시각(없으면 그날 하원 시각)이 가장 이른 하나를 잠금 없이 조회한다. 없으면 null
    /// </summary>
    public async Task<LivePlanRow?> GetLivePlanAsync(int userId, DateOnly date, DateTime now,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var dp = new DynamicParameters();
            dp.Add("userId", userId);
            dp.Add("date", date.ToDateTime(TimeOnly.MinValue), DbType.Date);
            dp.Add("now", now);

            var sql = LivePlanSelectSql + $@"
                WHERE p.USER_ID = @userId
                  AND p.PICKUP_DATE = @date
                  AND p.STATUS IN ({LivePlanStatuses})
                  AND p.EXPIRE_DT > @now
                ORDER BY COALESCE(p.SLOT_DT, TIMESTAMP(p.PICKUP_DATE, p.END_TIME)), p.PICKUP_PLAN_ID
                LIMIT 1";

            return await _dapper.QueryFirstOrDefaultAsync<LivePlanRow>(
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
    /// 오늘 계획 한 건을 상태와 상관없이 조회한다(응답 만들기용). 없으면 null
    /// </summary>
    public async Task<LivePlanRow?> GetPlanByIdAsync(int pickupPlanId, IDbTransaction? transaction,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var dp = new DynamicParameters();
            dp.Add("pickupPlanId", pickupPlanId);

            var sql = LivePlanSelectSql + @"
                WHERE p.PICKUP_PLAN_ID = @pickupPlanId";

            return await _dapper.QueryFirstOrDefaultAsync<LivePlanRow>(
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
    /// 시간표 요일·날짜로 오늘 계획 ID를 조회한다. UNIQUE(TIMETABLE_DAY_ID, PICKUP_DATE)와 같은 키라 상태 조건을 붙이지 않는다
    /// (끝난 계획은 TIMETABLE_DAY_ID가 NULL이라 걸리지 않는다). 없으면 null
    /// </summary>
    public async Task<int?> GetLivePlanIdByTimetableDayAsync(int timetableDayId, DateOnly date,
        IDbTransaction? transaction, CancellationToken cancellationToken = default)
    {
        try
        {
            var dp = new DynamicParameters();
            dp.Add("timetableDayId", timetableDayId);
            dp.Add("date", date.ToDateTime(TimeOnly.MinValue), DbType.Date);

            var sql = @"
                SELECT p.PICKUP_PLAN_ID
                FROM PICKUP_PLAN AS p
                WHERE p.TIMETABLE_DAY_ID = @timetableDayId
                  AND p.PICKUP_DATE = @date";

            return await _dapper.QueryFirstOrDefaultAsync<int?>(
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
    /// 학원가 행을 FOR UPDATE로 잠근다. 배정·재배정 트랜잭션의 첫 문장으로 부른다. 행이 없으면 false
    /// </summary>
    public async Task<bool> LockAreaAsync(int areaId, IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var dp = new DynamicParameters();
            dp.Add("areaId", areaId);

            // 잠금 조회에는 DELETE_YN 조건을 붙이지 않는다(schema.md)
            var sql = @"
                SELECT AREA_ID
                FROM AREA
                WHERE AREA_ID = @areaId
                FOR UPDATE";

            var locked = await _dapper.QueryFirstOrDefaultAsync<int?>(
                new CommandDefinition(sql, dp, transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
            return locked is not null;
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
    /// 시간표 요일 행을 FOR UPDATE로 잠그고 시간표·아이·학원·그날 예외를 함께 읽는다. 잠그는 행은 TIMETABLE_DAY 하나다.
    /// DELETE_YN 조건을 붙이지 않고 Alive로 돌려준다. 행이 없으면 null
    /// </summary>
    public async Task<TimetableDayLockRow?> LockTimetableDayAsync(int timetableDayId, DateOnly date,
        IDbTransaction transaction, CancellationToken cancellationToken = default)
    {
        try
        {
            var dp = new DynamicParameters();
            dp.Add("timetableDayId", timetableDayId);
            dp.Add("date", date.ToDateTime(TimeOnly.MinValue), DbType.Date);

            // FOR UPDATE OF td — 함께 읽는 학원 행은 여러 부모가 같이 쓰므로 잠그지 않는다(배정은 AREA 잠금으로 이미 직렬화된다).
            // 잠금 조회라 DELETE_YN 조건 대신 Alive로 돌려준다. 예외는 살아 있는 그날 행만 본다
            var sql = @"
                SELECT td.TIMETABLE_DAY_ID AS TimetableDayId,
                       c.USER_ID AS UserId,
                       c.CHILD_ID AS ChildId,
                       a.ACADEMY_ID AS AcademyId,
                       a.AREA_ID AS AreaId,
                       a.LAT AS AcademyLat,
                       a.LNG AS AcademyLng,
                       td.PICKUP_YN AS PickupYn,
                       td.LAST_PICKUP_DATE AS LastPickupDate,
                       CASE WHEN e.EXCEPTION_TYPE = 'TIME_CHANGE' THEN e.CHANGED_END_TIME ELSE t.END_TIME END AS EndTime,
                       (e.EXCEPTION_TYPE IS NOT NULL AND e.EXCEPTION_TYPE = 'SKIP') AS Skipped,
                       (td.DELETE_YN = FALSE AND t.DELETE_YN = FALSE AND c.DELETE_YN = FALSE) AS Alive,
                       t.PICKUP_SPOT_ID AS PreferredSpotId
                FROM TIMETABLE_DAY AS td
                INNER JOIN TIMETABLE AS t
                        ON t.TIMETABLE_ID = td.TIMETABLE_ID
                INNER JOIN CHILD AS c
                        ON c.CHILD_ID = t.CHILD_ID
                INNER JOIN ACADEMY AS a
                        ON a.ACADEMY_ID = t.ACADEMY_ID
                LEFT JOIN TIMETABLE_EXCEPTION AS e
                       ON e.TIMETABLE_DAY_ID = td.TIMETABLE_DAY_ID
                      AND e.TARGET_DATE = @date
                      AND e.DELETE_YN = FALSE
                WHERE td.TIMETABLE_DAY_ID = @timetableDayId
                FOR UPDATE OF td";

            return await _dapper.QueryFirstOrDefaultAsync<TimetableDayLockRow>(
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
    /// 지점에서 배정 시각 앞뒤 windowMin분 안에 있는 진행 중 계획(끝나지 않은 STATUS, EXPIRE_DT > now) 수를 센다.
    /// excludePlanId가 있으면 그 계획은 빼고 센다
    /// </summary>
    public async Task<int> CountSlotPlansAsync(int pickupSpotId, DateTime slotDt, int windowMin, DateTime now,
        int? excludePlanId, IDbTransaction? transaction, CancellationToken cancellationToken = default)
    {
        try
        {
            var dp = new DynamicParameters();
            dp.Add("pickupSpotId", pickupSpotId);
            dp.Add("fromDt", slotDt.AddMinutes(-windowMin));
            dp.Add("toDt", slotDt.AddMinutes(windowMin));
            dp.Add("now", now);
            dp.Add("excludePlanId", excludePlanId);

            // IX_PICKUP_PLAN_01 (PICKUP_SPOT_ID, SLOT_DT) 범위 조회. 같은 학원가의 AREA 잠금을 쥔 트랜잭션 안에서 일반 조회로 센다
            var sql = $@"
                SELECT COUNT(*)
                FROM PICKUP_PLAN AS p
                WHERE p.PICKUP_SPOT_ID = @pickupSpotId
                  AND p.SLOT_DT BETWEEN @fromDt AND @toDt
                  AND p.STATUS IN ({LivePlanStatuses})
                  AND p.EXPIRE_DT > @now
                  AND (@excludePlanId IS NULL OR p.PICKUP_PLAN_ID <> @excludePlanId)";

            return await _dapper.ExecuteScalarAsync<int>(
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
    /// 오늘 계획 행을 PK 인덱스로 FOR UPDATE 잠그고 읽는다. 재배정 트랜잭션에서 AREA 잠금 다음에 부른다.
    /// DELETE_YN 조건을 붙이지 않으므로 끝난 계획도 돌려준다(UserId가 NULL). 행이 없으면 null
    /// </summary>
    public async Task<PlanLockRow?> LockPickupPlanByIdAsync(int pickupPlanId, IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var dp = new DynamicParameters();
            dp.Add("pickupPlanId", pickupPlanId);

            // FORCE INDEX (PRIMARY) — 보조 인덱스로 잠그면 계획 끝내기(PK 먼저)와 교착한다(schema.md)
            var sql = @"
                SELECT p.PICKUP_PLAN_ID AS PickupPlanId,
                       p.USER_ID AS UserId,
                       p.ACADEMY_ID AS AcademyId,
                       p.AREA_ID AS AreaId,
                       p.PICKUP_DATE AS PickupDate,
                       p.END_TIME AS EndTime,
                       p.STATUS AS Status,
                       p.PICKUP_SPOT_ID AS PickupSpotId,
                       p.SLOT_DT AS SlotDt,
                       p.CUSTOM_LAT AS CustomLat,
                       p.EXPIRE_DT AS ExpireDt,
                       p.TIMETABLE_DAY_ID AS TimetableDayId
                FROM PICKUP_PLAN AS p FORCE INDEX (PRIMARY)
                WHERE p.PICKUP_PLAN_ID = @pickupPlanId
                FOR UPDATE";

            return await _dapper.QueryFirstOrDefaultAsync<PlanLockRow>(
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
    /// 오늘 계획의 임의 좌표 도보 경로 선을 바꾼다. lineStringWkt는 SRID 4326 WKT("LINESTRING(위도 경도,…)")이고 null이면 비운다
    /// </summary>
    public async Task SetCustomWalkPathAsync(int pickupPlanId, string? lineStringWkt, IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var dp = new DynamicParameters();
            dp.Add("pickupPlanId", pickupPlanId);
            dp.Add("wkt", lineStringWkt);

            // CUSTOM_WALK_PATH는 스캐폴드가 건너뛴 칸이라 Dapper로 쓴다. SRID 4326 WKT는 위도·경도 축 순서다
            var sql = @"
                UPDATE PICKUP_PLAN
                SET CUSTOM_WALK_PATH = CASE WHEN @wkt IS NULL THEN NULL ELSE ST_GeomFromText(@wkt, 4326) END
                WHERE PICKUP_PLAN_ID = @pickupPlanId";

            await _dapper.ExecuteAsync(
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
    /// 오늘 계획에서 그 차량 이벤트 번호로 이미 들어간 이벤트의 유형을 조회한다. 없으면 null
    /// </summary>
    public async Task<string?> GetPlanEventTypeAsync(int pickupPlanId, Guid clientEventId,
        IDbTransaction transaction, CancellationToken cancellationToken = default)
    {
        try
        {
            var dp = new DynamicParameters();
            dp.Add("pickupPlanId", pickupPlanId);
            // CHAR(36)에 소문자 하이픈 형식으로 저장된다
            dp.Add("clientEventId", clientEventId.ToString("D"));

            // UK_PICKUP_EVENT_01 (PICKUP_PLAN_ID, CLIENT_EVENT_ID)라 많아야 한 행이다. 픽업 계열 테이블이라 DELETE_YN 조건을 붙이지 않는다
            var sql = @"
                SELECT e.EVENT_TYPE
                FROM PICKUP_EVENT AS e
                WHERE e.PICKUP_PLAN_ID = @pickupPlanId
                  AND e.CLIENT_EVENT_ID = @clientEventId";

            return await _dapper.QueryFirstOrDefaultAsync<string?>(
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
    /// 오늘 계획에서 그 차량 이벤트 번호로 들어간 이벤트의 유형과 DETAIL을 잠금 없이 조회한다. 끝난 계획의 이벤트도 찾는다. 없으면 null
    /// </summary>
    public async Task<PlanEventRow?> GetPlanEventByClientIdAsync(int pickupPlanId, Guid clientEventId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var dp = new DynamicParameters();
            dp.Add("pickupPlanId", pickupPlanId);
            // CHAR(36)에 소문자 하이픈 형식으로 저장된다
            dp.Add("clientEventId", clientEventId.ToString("D"));

            // UK_PICKUP_EVENT_01 (PICKUP_PLAN_ID, CLIENT_EVENT_ID)라 많아야 한 행이다.
            // 끝난 계획의 이벤트(DELETE_YN = 1)도 찾아야 하므로 DELETE_YN 조건을 붙이지 않는다
            var sql = @"
                SELECT e.EVENT_TYPE AS EventType,
                       e.DETAIL AS Detail
                FROM PICKUP_EVENT AS e
                WHERE e.PICKUP_PLAN_ID = @pickupPlanId
                  AND e.CLIENT_EVENT_ID = @clientEventId";

            return await _dapper.QueryFirstOrDefaultAsync<PlanEventRow>(
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
    /// 잠근 오늘 계획의 운전자 이름(USER_INFO.USER_NAME, 취소 문자에서는 부모 이름 자리)과 아이 번호(암호문)를 조회한다. 계획이 없거나 끝났거나 살아 있는 계정이 없으면 null.
    /// 아이가 없거나 삭제됐으면 ChildPhoneEnc는 null이다
    /// </summary>
    public async Task<CancelNoticeTargetRow?> GetCancelNoticeTargetAsync(int pickupPlanId, IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var dp = new DynamicParameters();
            dp.Add("pickupPlanId", pickupPlanId);

            // 호출자가 계획 행을 잠갔으므로 USER_ID·CHILD_ID는 바뀌지 않는다. 끝난 계획은 USER_ID가 NULL이라 걸리지 않는다
            var sql = @"
                SELECT u.USER_NAME AS UserName,
                       c.PHONE_ENC AS ChildPhoneEnc
                FROM PICKUP_PLAN AS p
                INNER JOIN USER_INFO AS u
                        ON u.USER_ID = p.USER_ID
                       AND u.DELETE_YN = FALSE
                LEFT JOIN CHILD AS c
                       ON c.CHILD_ID = p.CHILD_ID
                      AND c.USER_ID = p.USER_ID
                      AND c.DELETE_YN = FALSE
                WHERE p.PICKUP_PLAN_ID = @pickupPlanId";

            return await _dapper.QueryFirstOrDefaultAsync<CancelNoticeTargetRow>(
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
    /// 픽업 지점의 정원을 조회한다. 지점이 없거나 삭제됐으면 null
    /// </summary>
    public async Task<byte?> GetPickupSpotCapacityAsync(int pickupSpotId, IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var dp = new DynamicParameters();
            dp.Add("pickupSpotId", pickupSpotId);

            // 이미 배정된 지점이라 USE_YN은 보지 않는다(늦은 출발은 다른 지점으로 옮기지 않는다)
            var sql = @"
                SELECT s.CAPACITY
                FROM PICKUP_SPOT AS s
                WHERE s.PICKUP_SPOT_ID = @pickupSpotId
                  AND s.DELETE_YN = FALSE";

            return await _dapper.QueryFirstOrDefaultAsync<byte?>(
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
    /// 만료 시각이 지난 진행 중 계획(끝나지 않은 STATUS, EXPIRE_DT &lt;= now)의 ID를 잠금 없이 PK 순으로 조회한다
    /// </summary>
    public async Task<List<int>> GetExpiredPlanIdsAsync(DateTime now, CancellationToken cancellationToken = default)
    {
        try
        {
            var dp = new DynamicParameters();
            dp.Add("now", now);

            // 일반 조회로 후보만 찾는다. 끝낼지는 계획마다 PK로 잠근 뒤 최신 값으로 다시 본다(schema.md 만료 처리)
            var sql = $@"
                SELECT p.PICKUP_PLAN_ID
                FROM PICKUP_PLAN AS p
                WHERE p.STATUS IN ({LivePlanStatuses})
                  AND p.EXPIRE_DT <= @now
                ORDER BY p.PICKUP_PLAN_ID";

            var rows = await _dapper.QueryAsync<int>(
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
    /// 오늘 계획을 끝내고 익명화한다. 호출자가 계획 행을 이미 PK로 잠갔다고 본다.
    /// 계획이 없거나 이미 끝났으면 아무것도 바꾸지 않고 false
    /// </summary>
    public async Task<bool> ClosePickupPlanAsync(int pickupPlanId, string closeStatus, bool setLastPickupDate,
        DateTime now, string updateUser, IDbTransaction transaction, CancellationToken cancellationToken = default,
        Guid? clientEventId = null, DateTime? eventDt = null, DateTime? boardDt = null)
    {
        try
        {
            var dp = new DynamicParameters();
            dp.Add("pickupPlanId", pickupPlanId);
            dp.Add("closeStatus", closeStatus);
            dp.Add("now", now);
            dp.Add("updateUser", updateUser);
            // 끝내는 차량 이벤트의 번호와 시각. 만료처럼 없으면 CLIENT_EVENT_ID NULL, EVENT_DT now다. CHAR(36)에 소문자 하이픈 형식으로 저장된다
            dp.Add("clientEventId", clientEventId?.ToString("D"));
            dp.Add("eventDt", eventDt ?? now);
            // 탑승 시각. 탑승만 넘기고, 만료·취소는 null이라 BOARD_DT를 그대로 둔다
            dp.Add("boardDt", boardDt);

            // 1) 계획이 가리키는 요일과 날짜를 읽는다. 호출자가 쥔 PK 잠금을 다시 잡아 최신 값을 읽는다(같은 트랜잭션이라 기다리지 않는다)
            var planSql = $@"
                SELECT p.TIMETABLE_DAY_ID AS TimetableDayId,
                       p.PICKUP_DATE AS PickupDate,
                       (p.STATUS IN ({LivePlanStatuses})) AS Live
                FROM PICKUP_PLAN AS p FORCE INDEX (PRIMARY)
                WHERE p.PICKUP_PLAN_ID = @pickupPlanId
                FOR UPDATE";
            var plan = await _dapper.QueryFirstOrDefaultAsync<ClosePlanRow>(
                new CommandDefinition(planSql, dp, transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
            if (plan is null || !plan.Live)
                return false;

            // 2) 계획 행을 고치기 전에 TIMETABLE_DAY를 잠근다 — 계획 PK → TIMETABLE_DAY → 계획 UPDATE 순서(schema.md 상태 바꾸기).
            //    잠금 조회라 DELETE_YN 조건을 붙이지 않는다
            if (plan.TimetableDayId is { } timetableDayId)
            {
                dp.Add("timetableDayId", timetableDayId);
                var lockDaySql = @"
                    SELECT td.TIMETABLE_DAY_ID
                    FROM TIMETABLE_DAY AS td
                    WHERE td.TIMETABLE_DAY_ID = @timetableDayId
                    FOR UPDATE";
                await _dapper.QueryFirstOrDefaultAsync<int?>(
                    new CommandDefinition(lockDaySql, dp, transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
            }

            // 3) 상태·끝난 시각·탑승 시각과 익명화(참조·하원 시각·도보 시간·임의 좌표 칸 NULL, DELETE_YN)를 한 UPDATE로 한다.
            //    나누면 WALK_MIN·CUSTOM_*를 비우는 순간 CK_PICKUP_PLAN_01·02에 걸린다(schema.md 익명화)
            var closeSql = $@"
                UPDATE PICKUP_PLAN
                SET STATUS = @closeStatus,
                    CLOSE_DT = @now,
                    BOARD_DT = COALESCE(@boardDt, BOARD_DT),
                    USER_ID = NULL,
                    CHILD_ID = NULL,
                    TIMETABLE_DAY_ID = NULL,
                    ACADEMY_ID = NULL,
                    END_TIME = NULL,
                    WALK_MIN = NULL,
                    CUSTOM_LAT = NULL,
                    CUSTOM_LNG = NULL,
                    CUSTOM_NAME = NULL,
                    CUSTOM_WALK_PATH = NULL,
                    DELETE_YN = 1,
                    UPDATE_DT = @now,
                    UPDATE_USER = @updateUser
                WHERE PICKUP_PLAN_ID = @pickupPlanId
                  AND STATUS IN ({LivePlanStatuses})";
            var closed = await _dapper.ExecuteAsync(
                new CommandDefinition(closeSql, dp, transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
            if (closed == 0)
                return false;

            // 4) 하원 시각이 든 END_TIME_CHANGED와 아이가 정한 출발 시각이 든 LEAVE_SCHEDULED 이벤트의 DETAIL을 비운다
            var clearDetailSql = @"
                UPDATE PICKUP_EVENT
                SET DETAIL = NULL,
                    UPDATE_DT = @now,
                    UPDATE_USER = @updateUser
                WHERE PICKUP_PLAN_ID = @pickupPlanId
                  AND EVENT_TYPE IN ('END_TIME_CHANGED', 'LEAVE_SCHEDULED')";
            await _dapper.ExecuteAsync(
                new CommandDefinition(clearDetailSql, dp, transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);

            // 5) 탑승·만료로 끝나면 그날 다시 만들지 않게 LAST_PICKUP_DATE를 계획의 날짜로 바꾼다(취소는 바꾸지 않는다). 더 늦은 날짜는 되돌리지 않는다
            if (setLastPickupDate && plan.TimetableDayId is not null)
            {
                // DateOnly를 그대로 넘기면 드라이버가 문화권 형식 문자열로 보낸다
                dp.Add("pickupDate", plan.PickupDate.ToDateTime(TimeOnly.MinValue), DbType.Date);
                var lastPickupSql = @"
                    UPDATE TIMETABLE_DAY
                    SET LAST_PICKUP_DATE = GREATEST(COALESCE(LAST_PICKUP_DATE, @pickupDate), @pickupDate),
                        UPDATE_DT = @now,
                        UPDATE_USER = @updateUser
                    WHERE TIMETABLE_DAY_ID = @timetableDayId";
                await _dapper.ExecuteAsync(
                    new CommandDefinition(lastPickupSql, dp, transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
            }

            // 6) 링크도 계획과 함께 끝난다
            var closeSessionSql = @"
                UPDATE PICKUP_SESSION
                SET CLOSE_DT = @now,
                    DELETE_YN = 1,
                    UPDATE_DT = @now,
                    UPDATE_USER = @updateUser
                WHERE PICKUP_PLAN_ID = @pickupPlanId";
            await _dapper.ExecuteAsync(
                new CommandDefinition(closeSessionSql, dp, transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);

            // 7) 이벤트 이력도 익명화 표시를 켠다
            var deleteEventsSql = @"
                UPDATE PICKUP_EVENT
                SET DELETE_YN = 1,
                    UPDATE_DT = @now,
                    UPDATE_USER = @updateUser
                WHERE PICKUP_PLAN_ID = @pickupPlanId";
            await _dapper.ExecuteAsync(
                new CommandDefinition(deleteEventsSql, dp, transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);

            // 8) 끝난 이벤트(BOARDED·EXPIRED·CANCELED)를 넣는다. 익명화 대상이라 DELETE_YN을 켠 채 넣는다
            var insertEventSql = @"
                INSERT INTO PICKUP_EVENT
                    (PICKUP_PLAN_ID, EVENT_TYPE, EVENT_DT, CLIENT_EVENT_ID, DETAIL,
                     CREATE_DT, CREATE_USER, UPDATE_DT, UPDATE_USER, DELETE_YN)
                VALUES
                    (@pickupPlanId, @closeStatus, @eventDt, @clientEventId, NULL,
                     @now, @updateUser, @now, @updateUser, 1)";
            await _dapper.ExecuteAsync(
                new CommandDefinition(insertEventSql, dp, transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);

            return true;
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
