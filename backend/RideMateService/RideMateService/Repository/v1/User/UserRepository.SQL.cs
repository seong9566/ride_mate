using System.Data;
using Dapper;

namespace RideMateService.Repository.v1.User;

public partial class UserRepository
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
    /// 그 계정의 집·차·초기 설정 완료 시각을 돌려준다. 계정이 없거나 지워졌으면 null이다. 쓰기 트랜잭션 안에서 부르면 그 트랜잭션을 넘긴다.
    /// </summary>
    public async Task<UserSetupRow?> GetUserSetupRowAsync(int userId, IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var dp = new DynamicParameters();
            dp.Add("userId", userId);

            var sql = @"
                SELECT t.DEST_ADDRESS AS DestAddress,
                       t.DEST_LAT AS DestLat,
                       t.DEST_LNG AS DestLng,
                       t.CAR_MODEL AS CarModel,
                       t.CAR_COLOR AS CarColor,
                       t.CAR_NO_LAST4 AS CarNoLast4,
                       t.SETUP_DONE_DT AS SetupDoneDt
                FROM USER_INFO AS t
                WHERE t.USER_ID = @userId
                  AND t.DELETE_YN = FALSE";

            return await _dapper.QueryFirstOrDefaultAsync<UserSetupRow>(
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
    /// 그 계정에 살아 있는 아이가 있는지 돌려준다.
    /// </summary>
    public async Task<bool> HasLiveChildAsync(int userId, IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var dp = new DynamicParameters();
            dp.Add("userId", userId);

            var sql = @"
                SELECT EXISTS (SELECT 1
                               FROM CHILD AS c
                               WHERE c.USER_ID = @userId
                                 AND c.DELETE_YN = FALSE)";

            var exists = await _dapper.QueryFirstAsync<int>(
                new CommandDefinition(sql, dp, transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
            return exists != 0;
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
    /// 그 계정의 살아 있는 아이의 살아 있는 시간표에 픽업이 켜진 살아 있는 요일이 하나라도 있는지 돌려준다.
    /// </summary>
    public async Task<bool> HasPickupOnScheduleAsync(int userId, IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var dp = new DynamicParameters();
            dp.Add("userId", userId);

            var sql = @"
                SELECT EXISTS (SELECT 1
                               FROM CHILD AS c
                               INNER JOIN TIMETABLE AS t
                                       ON t.CHILD_ID = c.CHILD_ID
                                      AND t.DELETE_YN = FALSE
                               INNER JOIN TIMETABLE_DAY AS td
                                       ON td.TIMETABLE_ID = t.TIMETABLE_ID
                                      AND td.DELETE_YN = FALSE
                                      AND td.PICKUP_YN = TRUE
                               WHERE c.USER_ID = @userId
                                 AND c.DELETE_YN = FALSE)";

            var exists = await _dapper.QueryFirstAsync<int>(
                new CommandDefinition(sql, dp, transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
            return exists != 0;
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
    /// 그 계정의 집·자동 발송 여부와 살아 있는 아이 한 명(CHILD_ID가 가장 작은 아이)을 돌려준다. 계정이 없거나 지워졌으면 null이다.
    /// </summary>
    public async Task<UserDetailRow?> GetUserDetailRowAsync(int userId, IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var dp = new DynamicParameters();
            dp.Add("userId", userId);

            var sql = @"
                SELECT t.DEST_ADDRESS AS DestAddress,
                       t.DEST_LAT AS DestLat,
                       t.DEST_LNG AS DestLng,
                       t.AUTO_SEND_YN AS AutoSendYn,
                       c.CHILD_ID AS ChildId,
                       c.CHILD_NAME AS ChildName,
                       c.PHONE_ENC AS PhoneEnc,
                       c.SEND_CHANNEL AS SendChannel
                FROM USER_INFO AS t
                LEFT JOIN CHILD AS c
                       ON c.CHILD_ID = (SELECT MIN(c2.CHILD_ID)
                                        FROM CHILD AS c2
                                        WHERE c2.USER_ID = t.USER_ID
                                          AND c2.DELETE_YN = FALSE)
                WHERE t.USER_ID = @userId
                  AND t.DELETE_YN = FALSE";

            return await _dapper.QueryFirstOrDefaultAsync<UserDetailRow>(
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
}
