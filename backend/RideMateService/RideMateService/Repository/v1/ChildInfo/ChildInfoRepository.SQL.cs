using System.Data;
using Dapper;
using RideMateService.Commons.DBModels;

namespace RideMateService.Repository.v1.ChildInfo;

public partial class ChildInfoRepository
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
    /// 그 계정·종류의 가장 최근 동의 행의 동의 여부를 돌려준다. 행이 없으면 null이다.
    /// </summary>
    public async Task<bool?> GetLatestConsentAgreeAsync(int userId, string consentType, IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var dp = new DynamicParameters();
            dp.Add("userId", userId);
            dp.Add("consentType", consentType);

            var sql = @"
                SELECT t.AGREE_YN
                FROM CONSENT AS t
                WHERE t.USER_ID = @userId
                  AND t.CONSENT_TYPE = @consentType
                  AND t.DELETE_YN = FALSE
                ORDER BY t.CONSENT_ID DESC
                LIMIT 1";

            return await _dapper.QueryFirstOrDefaultAsync<bool?>(
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
    /// 그 계정의 살아 있는 아이를 돌려준다. 없으면 null이다. 아이 ID·이름·암호화한 번호·발송 채널만 채워진다.
    /// </summary>
    public async Task<Child?> GetLiveChildAsync(int userId, IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var dp = new DynamicParameters();
            dp.Add("userId", userId);

            var sql = @"
                SELECT t.CHILD_ID AS ChildId,
                       t.CHILD_NAME AS ChildName,
                       t.PHONE_ENC AS PhoneEnc,
                       t.SEND_CHANNEL AS SendChannel
                FROM CHILD AS t
                WHERE t.USER_ID = @userId
                  AND t.DELETE_YN = FALSE
                ORDER BY t.CHILD_ID
                LIMIT 1";

            return await _dapper.QueryFirstOrDefaultAsync<Child>(
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
    /// 같은 번호 해시의 살아 있는 아이가 (어느 계정에든) 있는지 돌려준다.
    /// </summary>
    public async Task<bool> ExistsLivePhoneHashAsync(string phoneHash, IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var dp = new DynamicParameters();
            dp.Add("phoneHash", phoneHash);

            var sql = @"
                SELECT t.CHILD_ID
                FROM CHILD AS t
                WHERE t.PHONE_HASH = @phoneHash
                  AND t.DELETE_YN = FALSE
                LIMIT 1";

            var childId = await _dapper.QueryFirstOrDefaultAsync<int?>(
                new CommandDefinition(sql, dp, transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
            return childId is not null;
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
