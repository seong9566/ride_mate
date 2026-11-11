using System.Data;
using Dapper;
using RideMateService.Comm.Dto.v1.Schedule.Response;
using RideMateService.Commons.DBModels;

namespace RideMateService.Repository.v1.Schedule;

public partial class ScheduleRepository
{
    /// <summary>
    /// 살아 있는 운전자 계정을 FOR UPDATE 잠그고 ID를 돌려준다. 같은 계정의 동시 요청을 직렬화한다.
    /// </summary>
    public async Task<int?> LockUserInfoAsync(int userId, IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var dp = new DynamicParameters();
            dp.Add("userId", userId);

            var sql = @"
                SELECT t.USER_ID
                FROM USER_INFO AS t
                WHERE t.USER_ID = @userId
                  AND t.DELETE_YN = FALSE
                FOR UPDATE";

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
    /// 그 계정의 살아 있는 아이 ID를 돌려준다. 없으면 null이다.
    /// </summary>
    public async Task<int?> GetLiveChildIdAsync(int userId, IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var dp = new DynamicParameters();
            dp.Add("userId", userId);

            var sql = @"
                SELECT t.CHILD_ID
                FROM CHILD AS t
                WHERE t.USER_ID = @userId
                  AND t.DELETE_YN = FALSE
                ORDER BY t.CHILD_ID
                LIMIT 1";

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
    /// 그 아이의 살아 있는 시간표 개수를 돌려준다.
    /// </summary>
    public async Task<int> CountLiveSchedulesAsync(int childId, IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var dp = new DynamicParameters();
            dp.Add("childId", childId);

            var sql = @"
                SELECT COUNT(*)
                FROM TIMETABLE AS t
                WHERE t.CHILD_ID = @childId
                  AND t.DELETE_YN = FALSE";

            return await _dapper.QueryFirstAsync<int>(
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
    /// 그 아이의 살아 있는 시간표를 (시간표, 살아 있는 요일) 한 건씩 하원 시각과 함께 돌려준다. 겹침 판정에 쓴다.
    /// </summary>
    public async Task<List<LiveScheduleTimeRow>> GetLiveScheduleTimesAsync(int childId, IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var dp = new DynamicParameters();
            dp.Add("childId", childId);

            var sql = @"
                SELECT t.TIMETABLE_ID AS TimetableId,
                       t.DISPLAY_NAME AS DisplayName,
                       td.DAY_OF_WEEK AS DayOfWeek,
                       t.END_TIME AS EndTime
                FROM TIMETABLE AS t
                INNER JOIN TIMETABLE_DAY AS td
                        ON td.TIMETABLE_ID = t.TIMETABLE_ID
                       AND td.DELETE_YN = FALSE
                WHERE t.CHILD_ID = @childId
                  AND t.DELETE_YN = FALSE
                ORDER BY td.DAY_OF_WEEK, t.END_TIME, t.TIMETABLE_ID";

            var rows = await _dapper.QueryAsync<LiveScheduleTimeRow>(
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
    /// 그 좌표를 담는 살아 있는 학원가의 ID를 돌려준다. 학원가 밖이면 null이다.
    /// </summary>
    public async Task<int?> FindAreaIdAsync(decimal lat, decimal lng, IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var dp = new DynamicParameters();
            dp.Add("lat", lat);
            dp.Add("lng", lng);

            // SRID 4326 WKT는 위도 경도 순이다
            var sql = @"
                SELECT a.AREA_ID
                FROM AREA AS a
                WHERE a.DELETE_YN = FALSE
                  AND ST_Contains(a.BOUNDARY, ST_GeomFromText(CONCAT('POINT(', @lat, ' ', @lng, ')'), 4326))
                ORDER BY a.AREA_ID
                LIMIT 1";

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
    /// 이름·주소가 같은 살아 있는 학원을 돌려준다. 없으면 null이고, 여럿이면 ID가 작은 것이다.
    /// 학원 ID·학원가 ID·이름·주소·좌표만 채워진다.
    /// </summary>
    public async Task<Academy?> FindAcademyAsync(string academyName, string address, IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var dp = new DynamicParameters();
            dp.Add("academyName", academyName);
            dp.Add("address", address);

            var sql = @"
                SELECT t.ACADEMY_ID AS AcademyId,
                       t.AREA_ID AS AreaId,
                       t.ACADEMY_NAME AS AcademyName,
                       t.ADDRESS AS Address,
                       t.LAT AS Lat,
                       t.LNG AS Lng
                FROM ACADEMY AS t
                WHERE t.ACADEMY_NAME = @academyName
                  AND t.ADDRESS = @address
                  AND t.DELETE_YN = FALSE
                ORDER BY t.ACADEMY_ID
                LIMIT 1";

            return await _dapper.QueryFirstOrDefaultAsync<Academy>(
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
    /// 그 아이의 살아 있는 시간표를 하원 시각·등록 순으로 요일과 함께 돌려준다. timetableId를 주면 그 시간표 한 건만 돌려준다.
    /// 쓰기 트랜잭션 안에서 부르면 그 트랜잭션을 넘긴다.
    /// </summary>
    public async Task<List<ScheduleItemDto>> GetScheduleItemsAsync(int childId, int? timetableId,
        IDbTransaction? transaction = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var dp = new DynamicParameters();
            dp.Add("childId", childId);
            dp.Add("timetableId", timetableId);

            // 요일이 없는 시간표도 빠지지 않도록 LEFT JOIN이다. 시간표 한 건에 요일이 여러 행으로 나온다
            var sql = @"
                SELECT t.TIMETABLE_ID AS scheduleSeq,
                       t.DISPLAY_NAME AS displayName,
                       a.ACADEMY_NAME AS academyName,
                       a.ADDRESS AS address,
                       a.LAT AS lat,
                       a.LNG AS lng,
                       TIME_FORMAT(t.END_TIME, '%H:%i') AS endTime,
                       td.TIMETABLE_DAY_ID AS scheduleDaySeq,
                       td.DAY_OF_WEEK AS dayOfWeek,
                       td.PICKUP_YN AS pickupYn
                FROM TIMETABLE AS t
                INNER JOIN ACADEMY AS a
                        ON a.ACADEMY_ID = t.ACADEMY_ID
                       AND a.DELETE_YN = FALSE
                LEFT JOIN TIMETABLE_DAY AS td
                       ON td.TIMETABLE_ID = t.TIMETABLE_ID
                      AND td.DELETE_YN = FALSE
                WHERE t.CHILD_ID = @childId
                  AND t.DELETE_YN = FALSE
                  AND (@timetableId IS NULL OR t.TIMETABLE_ID = @timetableId)
                ORDER BY t.END_TIME, t.TIMETABLE_ID, td.DAY_OF_WEEK";

            var items = new Dictionary<int, ScheduleItemDto>();
            var ordered = new List<ScheduleItemDto>();
            await _dapper.QueryAsync<ScheduleItemDto, ScheduleDayDto?, ScheduleItemDto>(
                new CommandDefinition(sql, dp, transaction, cancellationToken: cancellationToken),
                (item, day) =>
                {
                    if (!items.TryGetValue(item.scheduleSeq, out var current))
                    {
                        current = item;
                        items[item.scheduleSeq] = current;
                        ordered.Add(current);
                    }

                    if (day is not null)
                        current.days.Add(day);
                    return current;
                },
                splitOn: "scheduleDaySeq").ConfigureAwait(false);
            return ordered;
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
