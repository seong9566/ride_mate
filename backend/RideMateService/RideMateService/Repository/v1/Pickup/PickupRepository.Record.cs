using System.Data;
using Dapper;
using Microsoft.EntityFrameworkCore;
using RideMateService.Commons.DBModels;

namespace RideMateService.Repository.v1.Pickup;

public partial class PickupRepository
{
    /// <summary>
    /// 살아 있는 운전자 계정을 FOR UPDATE 잠그고 ID를 돌려준다. 픽업 완료 기록 저장 트랜잭션의 첫 문장으로 부른다.
    /// 시간표 삭제·편집(USER_INFO → … → TIMETABLE_DAY)과 같은 순서라 기록 INSERT의 USER_INFO FK 공유 잠금이 교착하지 않는다
    /// </summary>
    public async Task<int?> LockRecordUserAsync(int userId, IDbTransaction transaction,
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
            throw; // Service가 500으로 바꾼다
        }
    }

    /// <summary>
    /// 로그인한 부모의 살아 있는 아이의 살아 있는 시간표 요일을 FOR UPDATE로 잠그고 ID를 돌려준다. 없거나 남의 것이면 null.
    /// 잠그는 행은 TIMETABLE_DAY 하나다
    /// </summary>
    public async Task<int?> LockRecordTimetableDayAsync(int userId, int timetableId, byte dayOfWeek,
        IDbTransaction transaction, CancellationToken cancellationToken = default)
    {
        try
        {
            var dp = new DynamicParameters();
            dp.Add("userId", userId);
            dp.Add("timetableId", timetableId);
            dp.Add("dayOfWeek", dayOfWeek);

            var sql = @"
                SELECT td.TIMETABLE_DAY_ID
                FROM TIMETABLE_DAY AS td
                INNER JOIN TIMETABLE AS t
                        ON t.TIMETABLE_ID = td.TIMETABLE_ID
                       AND t.DELETE_YN = FALSE
                INNER JOIN CHILD AS c
                        ON c.CHILD_ID = t.CHILD_ID
                       AND c.USER_ID = @userId
                       AND c.DELETE_YN = FALSE
                WHERE td.TIMETABLE_ID = @timetableId
                  AND td.DAY_OF_WEEK = @dayOfWeek
                  AND td.DELETE_YN = FALSE
                FOR UPDATE OF td";

            return await _dapper.QueryFirstOrDefaultAsync<int?>(
                new CommandDefinition(sql, dp, transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested) // 요청 취소는 로그 없이 올려보낸다
        {
            _logService.LogMessage(ex.ToString());
#if DEBUG
            _logService.ConsoleWarning(ex.ToString());
#endif
            throw; // Service가 500으로 바꾼다
        }
    }

    /// <summary>
    /// 로그인한 부모의 살아 있는 아이의 살아 있는 시간표 요일 ID를 잠그지 않고 조회한다. 없거나 남의 것이면 null
    /// </summary>
    public async Task<int?> GetRecordTimetableDayIdAsync(int userId, int timetableId, byte dayOfWeek,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var dp = new DynamicParameters();
            dp.Add("userId", userId);
            dp.Add("timetableId", timetableId);
            dp.Add("dayOfWeek", dayOfWeek);

            var sql = @"
                SELECT td.TIMETABLE_DAY_ID
                FROM TIMETABLE_DAY AS td
                INNER JOIN TIMETABLE AS t
                        ON t.TIMETABLE_ID = td.TIMETABLE_ID
                       AND t.DELETE_YN = FALSE
                INNER JOIN CHILD AS c
                        ON c.CHILD_ID = t.CHILD_ID
                       AND c.USER_ID = @userId
                       AND c.DELETE_YN = FALSE
                WHERE td.TIMETABLE_ID = @timetableId
                  AND td.DAY_OF_WEEK = @dayOfWeek
                  AND td.DELETE_YN = FALSE";

            return await _dapper.QueryFirstOrDefaultAsync<int?>(
                new CommandDefinition(sql, dp, cancellationToken: cancellationToken)).ConfigureAwait(false);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested) // 요청 취소는 로그 없이 올려보낸다
        {
            _logService.LogMessage(ex.ToString());
#if DEBUG
            _logService.ConsoleWarning(ex.ToString());
#endif
            throw; // Service가 500으로 바꾼다
        }
    }

    /// <summary>
    /// 시간표 요일의 살아 있는 픽업 완료 기록을 조회한다. 없으면 null
    /// </summary>
    public async Task<PickupRecordRow?> GetPickupRecordRowAsync(int timetableDayId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var dp = new DynamicParameters();
            dp.Add("timetableDayId", timetableDayId);

            // UK_PICKUP_RECORD_01 (TIMETABLE_DAY_ID, ACTIVE_FLAG) 조회
            var sql = @"
                SELECT r.ARRIVE_DT AS ArriveDt,
                       r.TOTAL_MIN AS TotalMin,
                       r.WAIT_MIN AS WaitMin,
                       r.DEPART_NAME AS DepartName,
                       r.DEPART_LAT AS DepartLat,
                       r.DEPART_LNG AS DepartLng,
                       r.DEPART_DT AS DepartDt,
                       r.BOARD_NAME AS BoardName,
                       r.BOARD_LAT AS BoardLat,
                       r.BOARD_LNG AS BoardLng,
                       r.BOARD_DT AS BoardDt,
                       r.AUTO_SAVE_YN AS AutoSaveYn,
                       r.UPDATE_DT AS UpdateDt
                FROM PICKUP_RECORD AS r
                WHERE r.TIMETABLE_DAY_ID = @timetableDayId
                  AND r.DELETE_YN = FALSE";

            return await _dapper.QueryFirstOrDefaultAsync<PickupRecordRow>(
                new CommandDefinition(sql, dp, cancellationToken: cancellationToken)).ConfigureAwait(false);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested) // 요청 취소는 로그 없이 올려보낸다
        {
            _logService.LogMessage(ex.ToString());
#if DEBUG
            _logService.ConsoleWarning(ex.ToString());
#endif
            throw; // Service가 500으로 바꾼다
        }
    }

    /// <summary>
    /// 시간표 요일의 살아 있는 픽업 완료 기록을 불러온다(수정 대상, 추적). 없으면 null
    /// </summary>
    public async Task<PickupRecord?> GetPickupRecordEntityAsync(int timetableDayId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _context.PickupRecords
                .FirstOrDefaultAsync(x => x.TimetableDayId == timetableDayId && x.DeleteYn == false, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested) // 요청 취소는 로그 없이 올려보낸다
        {
            _logService.LogMessage(ex.ToString());
#if DEBUG
            _logService.ConsoleWarning(ex.ToString());
#endif
            throw; // Service가 500으로 바꾼다
        }
    }

    /// <summary>
    /// 픽업 완료 기록 등록
    /// </summary>
    public async Task<bool> AddPickupRecordAsync(PickupRecord model, CancellationToken cancellationToken = default)
    {
        try
        {
            await _context.PickupRecords.AddAsync(model, cancellationToken).ConfigureAwait(false);

            return await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false) > 0;
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested) // 요청 취소는 로그 없이 올려보낸다
        {
            _logService.LogMessage(ex.ToString());
#if DEBUG
            _logService.ConsoleWarning(ex.ToString());
#endif
            throw; // Service가 500으로 바꾼다
        }
    }

    /// <summary>
    /// 픽업 완료 기록 수정(덮어쓰기)
    /// </summary>
    public async Task<bool> UpdatePickupRecordAsync(PickupRecord model, CancellationToken cancellationToken = default)
    {
        try
        {
            _context.PickupRecords.Update(model);

            return await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false) > 0;
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested) // 요청 취소는 로그 없이 올려보낸다
        {
            _logService.LogMessage(ex.ToString());
#if DEBUG
            _logService.ConsoleWarning(ex.ToString());
#endif
            throw; // Service가 500으로 바꾼다
        }
    }
}
