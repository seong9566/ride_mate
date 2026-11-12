using System.Data;
using Dapper;
using RideMateService.Comm.Dto.v1.Auth.Response;

namespace RideMateService.Repository.v1.Auth;

public partial class AuthRepository
{
    /// <summary>
    /// 토큰 해시로 만료되지 않은 WEB 세션과 살아 있는 계정을 조회한다.
    /// </summary>
    public async Task<(int loginSessionId, int userId)?> GetWebSessionAsync(string tokenHash, DateTime thisTime,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var dp = new DynamicParameters();
            dp.Add("tokenHash", tokenHash);
            dp.Add("thisTime", thisTime);

            var sql = @"
                SELECT s.LOGIN_SESSION_ID AS LoginSessionId,
                       s.USER_ID AS UserId
                FROM LOGIN_SESSION AS s
                INNER JOIN USER_INFO AS u ON u.USER_ID = s.USER_ID
                WHERE s.TOKEN_HASH = @tokenHash
                  AND s.SESSION_TYPE = 'WEB'
                  AND s.DELETE_YN = FALSE
                  AND s.EXPIRE_DT > @thisTime
                  AND u.DELETE_YN = FALSE";

            var row = await _dapper.QueryFirstOrDefaultAsync<WebSessionRow>(
                new CommandDefinition(sql, dp, cancellationToken: cancellationToken)).ConfigureAwait(false);
            return row is null ? null : (row.LoginSessionId, row.UserId);
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
    /// device_code 해시로 살아 있는 QR 로그인 요청을 찾아 FOR UPDATE로 잠그고 ID를 돌려준다.
    /// </summary>
    public async Task<(int qrLoginId, int? userId)?> LockQrLoginByDeviceCodeHashAsync(string hash, IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var dp = new DynamicParameters();
            dp.Add("hash", hash);

            var sql = @"
                SELECT t.QR_LOGIN_ID AS QrLoginId,
                       t.USER_ID AS UserId
                FROM QR_LOGIN AS t
                WHERE t.DEVICE_CODE_HASH = @hash
                  AND t.DELETE_YN = FALSE
                FOR UPDATE";

            var row = await _dapper.QueryFirstOrDefaultAsync<QrLoginLockRow>(
                new CommandDefinition(sql, dp, transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
            return row is null ? null : (row.QrLoginId, row.UserId);
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
    /// user_code 해시로 살아 있는 QR 로그인 요청을 찾아 FOR UPDATE로 잠그고 ID를 돌려준다.
    /// </summary>
    public async Task<int?> LockQrLoginByUserCodeHashAsync(string userCodeHash, IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var dp = new DynamicParameters();
            dp.Add("hash", userCodeHash);

            var sql = @"
                SELECT t.QR_LOGIN_ID
                FROM QR_LOGIN AS t
                WHERE t.USER_CODE_HASH = @hash
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
    /// 살아 있는 계정을 PK로 FOR UPDATE 잠그고 ID를 돌려준다. 차량 세션 생성 경로를 계정 단위로 직렬화한다.
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
    /// 전화번호 해시로 살아 있는 계정을 FOR UPDATE 잠그고 ID를 돌려준다.
    /// </summary>
    public async Task<int?> LockUserInfoByPhoneHashAsync(string phoneHash, IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var dp = new DynamicParameters();
            dp.Add("phoneHash", phoneHash);

            var sql = @"
                SELECT t.USER_ID
                FROM USER_INFO AS t
                WHERE t.PHONE_HASH = @phoneHash
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
    /// 살아 있는 WEB 세션을 PK로 FOR UPDATE 잠그고 ID를 돌려준다.
    /// </summary>
    public async Task<int?> LockWebLoginSessionAsync(int loginSessionId, IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var dp = new DynamicParameters();
            dp.Add("loginSessionId", loginSessionId);

            var sql = @"
                SELECT t.LOGIN_SESSION_ID
                FROM LOGIN_SESSION AS t
                WHERE t.LOGIN_SESSION_ID = @loginSessionId
                  AND t.SESSION_TYPE = 'WEB'
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
    /// 전화번호 해시로 살아 있는 가장 최근 휴대폰 인증번호를 찾아 FOR UPDATE로 잠그고 ID를 돌려준다.
    /// </summary>
    public async Task<int?> LockLatestPhoneVerificationAsync(string phoneHash, IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var dp = new DynamicParameters();
            dp.Add("phoneHash", phoneHash);

            var sql = @"
                SELECT t.PHONE_VERIFICATION_ID
                FROM PHONE_VERIFICATION AS t
                WHERE t.PHONE_HASH = @phoneHash
                  AND t.DELETE_YN = FALSE
                ORDER BY t.PHONE_VERIFICATION_ID DESC
                LIMIT 1
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
    /// 전화번호 해시와 용도로 살아 있는 가장 최근 휴대폰 인증번호를 찾아 FOR UPDATE로 잠그고 ID를 돌려준다.
    /// </summary>
    public async Task<int?> LockLatestPhoneVerificationByPurposeAsync(string phoneHash, string purpose,
        IDbTransaction transaction, CancellationToken cancellationToken = default)
    {
        try
        {
            var dp = new DynamicParameters();
            dp.Add("phoneHash", phoneHash);
            dp.Add("purpose", purpose);

            var sql = @"
                SELECT t.PHONE_VERIFICATION_ID
                FROM PHONE_VERIFICATION AS t
                WHERE t.PHONE_HASH = @phoneHash
                  AND t.PURPOSE = @purpose
                  AND t.DELETE_YN = FALSE
                ORDER BY t.PHONE_VERIFICATION_ID DESC
                LIMIT 1
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
    /// 같은 계정·기기의 살아 있는 차량 로그인 세션 ID를 FOR UPDATE 잠금 읽기로 조회한다.
    /// 호출 전에 USER_INFO 행을 PK로 잠가 차량 세션 생성 경로를 직렬화하며, 잠금 읽기라 최신 커밋을 본다.
    /// </summary>
    public async Task<IReadOnlyList<int>> GetCarSessionIdsAsync(int userId, string deviceId, IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var dp = new DynamicParameters();
            dp.Add("userId", userId);
            dp.Add("deviceId", deviceId);

            // USER_INFO PK 잠금으로 같은 계정의 차량 세션 생성 경로가 직렬화돼 있다. 잠금 읽기로 최신 커밋을 본다
            var sql = @"
                SELECT t.LOGIN_SESSION_ID
                FROM LOGIN_SESSION AS t
                WHERE t.USER_ID = @userId
                  AND t.DEVICE_ID = @deviceId
                  AND t.SESSION_TYPE = 'CAR'
                  AND t.DELETE_YN = FALSE
                FOR UPDATE";

            var ids = await _dapper.QueryAsync<int>(
                new CommandDefinition(sql, dp, transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
            return ids.ToList();
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
    /// 기기의 살아 있는 차량 로그인 세션을 계정 이름과 함께 최근 사용 순으로 조회한다(삭제된 계정·만료·로그아웃 세션 제외, 최대 limit개)
    /// </summary>
    public async Task<List<GetLoginSessionList>> GetLoginSessionListAsync(string deviceId, DateTime thisTime, int limit,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var dp = new DynamicParameters();
            dp.Add("deviceId", deviceId);
            dp.Add("thisTime", thisTime);
            dp.Add("limit", limit);

            var sql = @"
                SELECT s.LOGIN_SESSION_ID AS loginSessionSeq,
                       u.USER_NAME        AS userName
                FROM LOGIN_SESSION AS s
                INNER JOIN USER_INFO AS u ON u.USER_ID = s.USER_ID
                WHERE s.DEVICE_ID = @deviceId
                  AND s.SESSION_TYPE = 'CAR'
                  AND s.DELETE_YN = FALSE
                  AND s.EXPIRE_DT > @thisTime
                  AND u.DELETE_YN = FALSE
                ORDER BY s.LAST_USED_DT DESC, s.LOGIN_SESSION_ID DESC
                LIMIT @limit";

            var rows = await _dapper.QueryAsync<GetLoginSessionList>(
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
    /// 계정의 초기 설정이 필요한지 조회한다. 운전자 이름이 비었거나 초기 설정 완료 시각이 없으면 true, 계정이 없거나 삭제됐으면 null
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
                        OR t.SETUP_DONE_DT IS NULL) AS setupRequired
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

    /// <summary>
    /// 정리 대상 휴대폰 인증번호 ID를 afterId보다 큰 것부터 ID 순으로 limit개까지 조회한다(인증 성공이 verifiedBefore 이전이거나, 인증 없이 EXPIRE_DT가 expiredBefore 이전인 살아 있는 행)
    /// </summary>
    public async Task<IReadOnlyList<int>> GetCleanupPhoneVerificationIdsAsync(DateTime verifiedBefore,
        DateTime expiredBefore, int afterId, int limit, CancellationToken cancellationToken = default)
    {
        try
        {
            var dp = new DynamicParameters();
            dp.Add("verifiedBefore", verifiedBefore);
            dp.Add("expiredBefore", expiredBefore);
            dp.Add("afterId", afterId);
            dp.Add("limit", limit);

            var sql = @"
                SELECT t.PHONE_VERIFICATION_ID
                FROM PHONE_VERIFICATION AS t
                WHERE t.PHONE_VERIFICATION_ID > @afterId
                  AND t.DELETE_YN = FALSE
                  AND ((t.VERIFY_DT IS NOT NULL AND t.VERIFY_DT < @verifiedBefore)
                       OR (t.VERIFY_DT IS NULL AND t.EXPIRE_DT < @expiredBefore))
                ORDER BY t.PHONE_VERIFICATION_ID
                LIMIT @limit";

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

    private sealed class QrLoginLockRow
    {
        public int QrLoginId { get; set; }

        public int? UserId { get; set; }
    }

    private sealed class WebSessionRow
    {
        public int LoginSessionId { get; set; }

        public int UserId { get; set; }
    }
}
