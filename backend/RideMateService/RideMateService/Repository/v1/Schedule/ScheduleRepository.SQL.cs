using System.Data;
using Dapper;
using RideMateService.Comm.Dto.v1.Schedule.Response;
using RideMateService.Commons.DBModels;

namespace RideMateService.Repository.v1.Schedule;

public partial class ScheduleRepository
{
    /// <summary>
    /// 살아 있는 운전자 계정 ID를 돌려준다. 없으면 null이다. 잠그지 않는 조회라 목록처럼 읽기만 하는 곳에 쓴다.
    /// </summary>
    public async Task<int?> GetLiveUserIdAsync(int userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var dp = new DynamicParameters();
            dp.Add("userId", userId);

            var sql = @"
                SELECT t.USER_ID
                FROM USER_INFO AS t
                WHERE t.USER_ID = @userId
                  AND t.DELETE_YN = FALSE";

            return await _dapper.QueryFirstOrDefaultAsync<int?>(
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
    /// 그 계정의 살아 있는 아이 ID를 돌려준다. 없으면 null이다. 쓰기 트랜잭션 안에서 부르면 그 트랜잭션을 넘긴다.
    /// </summary>
    public async Task<int?> GetLiveChildIdAsync(int userId, IDbTransaction? transaction = null,
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
    /// 그 아이의 살아 있는 시간표에 속한 살아 있는 요일 한 건을 돌려준다. 없거나 남의 것이면 null이다.
    /// </summary>
    public async Task<ScheduleDayDto?> GetLiveScheduleDayAsync(int childId, int scheduleDaySeq,
        IDbTransaction transaction, CancellationToken cancellationToken = default)
    {
        try
        {
            var dp = new DynamicParameters();
            dp.Add("childId", childId);
            dp.Add("scheduleDaySeq", scheduleDaySeq);

            var sql = @"
                SELECT td.TIMETABLE_DAY_ID AS scheduleDaySeq,
                       td.DAY_OF_WEEK AS dayOfWeek,
                       td.PICKUP_YN AS pickupYn
                FROM TIMETABLE_DAY AS td
                INNER JOIN TIMETABLE AS t
                        ON t.TIMETABLE_ID = td.TIMETABLE_ID
                       AND t.DELETE_YN = FALSE
                WHERE td.TIMETABLE_DAY_ID = @scheduleDaySeq
                  AND td.DELETE_YN = FALSE
                  AND t.CHILD_ID = @childId";

            return await _dapper.QueryFirstOrDefaultAsync<ScheduleDayDto>(
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
    /// 그 좌표를 담는 살아 있는 학원가의 ID를 돌려준다. 학원가 밖이면 null이다. 트랜잭션 밖에서 부를 때는 넘기지 않는다.
    /// </summary>
    public async Task<int?> FindAreaIdAsync(decimal lat, decimal lng, IDbTransaction? transaction = null,
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

    /// <summary>
    /// 그 계정의 살아 있는 아이의 살아 있는 시간표가 가리키는 학원의 학원가 ID를 돌려준다.
    /// 시간표가 없거나 남의 것이거나 학원에 좌표가 없으면 null이다. 트랜잭션 없이 부르면 잠글 학원가를 고르는 일반 조회이고,
    /// 트랜잭션을 넘기면 FOR SHARE 잠금 읽기로 최신 값을 읽고 그 아이·시간표·학원 행을 잠근다(학원가가 채워지지 못하게 한다).
    /// </summary>
    public async Task<int?> GetScheduleAreaIdAsync(int userId, int timetableId, IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var dp = new DynamicParameters();
            dp.Add("userId", userId);
            dp.Add("timetableId", timetableId);

            var sql = @"
                SELECT a.AREA_ID
                FROM TIMETABLE AS t
                INNER JOIN CHILD AS c
                        ON c.CHILD_ID = t.CHILD_ID
                       AND c.DELETE_YN = FALSE
                INNER JOIN ACADEMY AS a
                        ON a.ACADEMY_ID = t.ACADEMY_ID
                       AND a.DELETE_YN = FALSE
                WHERE t.TIMETABLE_ID = @timetableId
                  AND t.DELETE_YN = FALSE
                  AND c.USER_ID = @userId";
            if (transaction is not null)
                sql += @"
                FOR SHARE";

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
    /// 학원가 행을 FOR UPDATE로 잠근다. 같은 학원가의 계획 생성과 시간표 삭제를 직렬화한다. 트랜잭션의 첫 문장으로 부른다.
    /// </summary>
    public async Task LockAreaAsync(int areaId, IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var dp = new DynamicParameters();
            dp.Add("areaId", areaId);

            // 잠금이 목적이라 schema.md 배정 1단계와 같은 문장을 쓴다(DELETE_YN 조건 없음)
            var sql = @"
                SELECT a.AREA_ID
                FROM AREA AS a
                WHERE a.AREA_ID = @areaId
                FOR UPDATE";

            await _dapper.QueryFirstOrDefaultAsync<int?>(
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
    /// 그 아이의 살아 있는 시간표이면 그 시간표의 요일 ID를 삭제 여부와 관계없이 모두 돌려준다. 없거나 남의 것이면 null이다.
    /// </summary>
    public async Task<List<int>?> GetLiveScheduleDayIdsAsync(int childId, int timetableId, IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var dp = new DynamicParameters();
            dp.Add("childId", childId);
            dp.Add("timetableId", timetableId);

            // 진행 중 계획 확인에 쓰므로 요일의 DELETE_YN은 거르지 않는다. 시간표가 있고 요일이 없으면 NULL 한 행이 나온다
            var sql = @"
                SELECT td.TIMETABLE_DAY_ID
                FROM TIMETABLE AS t
                LEFT JOIN TIMETABLE_DAY AS td
                       ON td.TIMETABLE_ID = t.TIMETABLE_ID
                WHERE t.TIMETABLE_ID = @timetableId
                  AND t.CHILD_ID = @childId
                  AND t.DELETE_YN = FALSE
                ORDER BY td.TIMETABLE_DAY_ID";

            var rows = (await _dapper.QueryAsync<int?>(
                new CommandDefinition(sql, dp, transaction, cancellationToken: cancellationToken)).ConfigureAwait(false))
                .ToList();
            return rows.Count == 0
                ? null
                : rows.Where(x => x is not null).Select(x => x!.Value).ToList();
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
    /// 그 아이의 살아 있는 시간표 한 건의 학원 ID·하원 시각을 돌려준다. 없거나 남의 것이면 null이다.
    /// </summary>
    public async Task<LiveScheduleRow?> GetLiveScheduleAsync(int childId, int timetableId, IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var dp = new DynamicParameters();
            dp.Add("childId", childId);
            dp.Add("timetableId", timetableId);

            var sql = @"
                SELECT t.TIMETABLE_ID AS TimetableId,
                       t.ACADEMY_ID AS AcademyId,
                       t.END_TIME AS EndTime
                FROM TIMETABLE AS t
                WHERE t.TIMETABLE_ID = @timetableId
                  AND t.CHILD_ID = @childId
                  AND t.DELETE_YN = FALSE";

            return await _dapper.QueryFirstOrDefaultAsync<LiveScheduleRow>(
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
    /// 그 시간표의 요일 행을 삭제 여부와 관계없이 모두 돌려준다. 편집에서 뺄 요일·되살릴 요일을 고르는 데 쓴다.
    /// </summary>
    public async Task<List<ScheduleDayRow>> GetScheduleDayRowsAsync(int timetableId, IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var dp = new DynamicParameters();
            dp.Add("timetableId", timetableId);

            // 되살릴 행과 진행 중 계획 확인에 쓰므로 요일의 DELETE_YN은 거르지 않는다
            var sql = @"
                SELECT td.TIMETABLE_DAY_ID AS TimetableDayId,
                       td.DAY_OF_WEEK AS DayOfWeek,
                       td.DELETE_YN AS DeleteYn
                FROM TIMETABLE_DAY AS td
                WHERE td.TIMETABLE_ID = @timetableId
                ORDER BY td.TIMETABLE_DAY_ID";

            var rows = await _dapper.QueryAsync<ScheduleDayRow>(
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
    /// 그 요일들을 가리키는 진행 중인 픽업 계획(PLANNED·STARTED·WAITING·STOPPED·LOOPING)의 ID를 일반 조회로 돌려준다. 잠그지 않는다.
    /// </summary>
    public async Task<List<int>> GetActivePickupPlanIdsAsync(IReadOnlyCollection<int> timetableDayIds,
        IDbTransaction transaction, CancellationToken cancellationToken = default)
    {
        try
        {
            var dp = new DynamicParameters();
            dp.Add("timetableDayIds", timetableDayIds);

            // PICKUP_PLAN은 DELETE_YN이 "끝나서 익명화됨"이라 조건을 붙이지 않고 STATUS로 가린다(database.md).
            // 끝난 계획은 TIMETABLE_DAY_ID가 NULL이라 걸리지 않는다.
            // 이 조회는 잠그지 않는다 — UK_PICKUP_PLAN_01(보조 인덱스)로 잠그면 계획을 끝내는 트랜잭션(계획 PK 잠금 → TIMETABLE_DAY_ID 비우기)과 교착한다
            var sql = @"
                SELECT p.PICKUP_PLAN_ID
                FROM PICKUP_PLAN AS p
                WHERE p.TIMETABLE_DAY_ID IN @timetableDayIds
                  AND p.STATUS IN ('PLANNED', 'STARTED', 'WAITING', 'STOPPED', 'LOOPING')
                ORDER BY p.PICKUP_PLAN_ID";

            var rows = await _dapper.QueryAsync<int>(
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
    /// 그 계획들을 PK 순서로 FOR UPDATE 잠그고, 최신 값으로 아직 그 요일들을 가리키는 진행 중 계획인 것의 ID만 돌려준다.
    /// </summary>
    public async Task<List<int>> LockActivePickupPlanIdsAsync(IReadOnlyCollection<int> pickupPlanIds,
        IReadOnlyCollection<int> timetableDayIds, IDbTransaction transaction, CancellationToken cancellationToken = default)
    {
        try
        {
            var dp = new DynamicParameters();
            dp.Add("pickupPlanIds", pickupPlanIds);
            dp.Add("timetableDayIds", timetableDayIds);

            // 잠금 읽기는 최신 커밋을 보므로 그사이 끝난 계획(STATUS가 바뀌고 TIMETABLE_DAY_ID가 NULL)은 빠진다.
            // 계획을 끝내는 트랜잭션과 같이 PK부터 잠그도록 PRIMARY를 강제한다(옵티마이저가 UK_PICKUP_PLAN_01을 고르면 교착한다)
            var sql = @"
                SELECT p.PICKUP_PLAN_ID
                FROM PICKUP_PLAN AS p FORCE INDEX (PRIMARY)
                WHERE p.PICKUP_PLAN_ID IN @pickupPlanIds
                  AND p.TIMETABLE_DAY_ID IN @timetableDayIds
                  AND p.STATUS IN ('PLANNED', 'STARTED', 'WAITING', 'STOPPED', 'LOOPING')
                ORDER BY p.PICKUP_PLAN_ID
                FOR UPDATE";

            var rows = await _dapper.QueryAsync<int>(
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
}
