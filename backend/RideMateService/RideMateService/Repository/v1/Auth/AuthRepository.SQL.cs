using System.Data;
using Dapper;

namespace RideMateService.Repository.v1.Auth;

public partial class AuthRepository
{
    /// <summary>
    /// refresh 토큰 해시로 살아 있는 차량 로그인 세션을 찾아 FOR UPDATE로 잠그고 ID를 돌려준다.
    /// 지금 값이거나, 허용 시간(graceFrom 이후) 안에 바뀐 직전 값이면 찾는다.
    /// 인덱스를 하나씩 타도록 지금 값(TOKEN_HASH)을 먼저 찾고, 없을 때만 직전 값(PREV_TOKEN_HASH)을 찾는다
    /// </summary>
    public async Task<int?> LockLoginSessionByRefreshAsync(string tokenHash, DateTime graceFrom, DateTime thisTime,
        IDbTransaction transaction, CancellationToken cancellationToken = default)
    {
        try
        {
            var dp = new DynamicParameters();
            dp.Add("tokenHash", tokenHash);
            dp.Add("graceFrom", graceFrom);
            dp.Add("thisTime", thisTime);

            // 한 문장에 OR로 묶으면 옵티마이저가 전체 스캔을 고를 수 있고, 그러면 FOR UPDATE가 모든 행을 잠근다
            var currentSql = @"
                SELECT t.LOGIN_SESSION_ID
                FROM LOGIN_SESSION AS t
                WHERE t.TOKEN_HASH = @tokenHash
                  AND t.SESSION_TYPE = 'CAR'
                  AND t.DELETE_YN = FALSE
                  AND t.EXPIRE_DT > @thisTime
                FOR UPDATE";

            var loginSessionId = await _dapper.QueryFirstOrDefaultAsync<int?>(
                new CommandDefinition(currentSql, dp, transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
            if (loginSessionId is not null)
                return loginSessionId;

            var prevSql = @"
                SELECT t.LOGIN_SESSION_ID
                FROM LOGIN_SESSION AS t
                WHERE t.PREV_TOKEN_HASH = @tokenHash
                  AND t.ROTATE_DT >= @graceFrom
                  AND t.SESSION_TYPE = 'CAR'
                  AND t.DELETE_YN = FALSE
                  AND t.EXPIRE_DT > @thisTime
                ORDER BY t.LOGIN_SESSION_ID
                LIMIT 1
                FOR UPDATE";

            return await _dapper.QueryFirstOrDefaultAsync<int?>(
                new CommandDefinition(prevSql, dp, transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
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
    /// 계정의 초기 설정이 필요한지 조회한다. 실명이 비었거나 살아 있는 아이가 없으면 true, 계정이 없거나 삭제됐으면 null
    /// </summary>
    public async Task<bool?> GetSetupRequiredAsync(int userId, IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var dp = new DynamicParameters();
            dp.Add("userId", userId);

            var sql = @"
                SELECT (t.USER_NAME = ''
                        OR NOT EXISTS (SELECT 1
                                       FROM CHILD AS c
                                       WHERE c.USER_ID = t.USER_ID
                                         AND c.DELETE_YN = FALSE)) AS setupRequired
                FROM USER_INFO AS t
                WHERE t.USER_ID = @userId
                  AND t.DELETE_YN = FALSE";

            var setupRequired = await _dapper.QueryFirstOrDefaultAsync<long?>(
                new CommandDefinition(sql, dp, transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);

            return setupRequired is null ? null : setupRequired.Value != 0;
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
