using Microsoft.EntityFrameworkCore;
using RideMateService.Commons.DBModels;

namespace RideMateService.Repository.v1.Auth;

public partial class AuthRepository
{
    /// <summary>
    /// 살아 있는 QR 로그인 요청을 PK로 불러온다(수정 대상)
    /// </summary>
    public async Task<QrLogin?> GetQrLoginAsync(int qrLoginId, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _context.QrLogins
                .FirstOrDefaultAsync(x => x.QrLoginId == qrLoginId
                                          && x.DeleteYn == false, cancellationToken)
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
    /// QR 로그인 요청 등록
    /// </summary>
    public async Task<bool> AddQrLoginAsync(QrLogin model, CancellationToken cancellationToken = default)
    {
        try
        {
            await _context.QrLogins.AddAsync(model, cancellationToken).ConfigureAwait(false);

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
    /// QR 로그인 요청 수정
    /// </summary>
    public async Task<bool> UpdateQrLoginAsync(QrLogin model, CancellationToken cancellationToken = default)
    {
        try
        {
            _context.QrLogins.Update(model);

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
    /// 살아 있는 운전자 계정을 PK로 불러온다.
    /// </summary>
    public async Task<UserInfo?> GetUserInfoAsync(int userId, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _context.UserInfos
                .FirstOrDefaultAsync(x => x.UserId == userId
                                          && x.DeleteYn == false, cancellationToken)
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
    /// 운전자 계정 등록. 저장 뒤 model의 운전자 계정 ID가 채워진다.
    /// </summary>
    public async Task<bool> AddUserInfoAsync(UserInfo model, CancellationToken cancellationToken = default)
    {
        try
        {
            await _context.UserInfos.AddAsync(model, cancellationToken).ConfigureAwait(false);

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
    /// 운전자 계정의 도착 기록 사용 여부만 바꾼다. 다른 컬럼은 건드리지 않고, 바뀐 행이 정확히 1개일 때 true를 돌려준다.
    /// </summary>
    public async Task<bool> UpdateUserArrivalLogAsync(int userId, bool arrivalLogYn, DateTime updateDt, string updateUser,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var affected = await _context.UserInfos
                .Where(x => x.UserId == userId
                            && x.DeleteYn == false)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.ArrivalLogYn, arrivalLogYn)
                    .SetProperty(x => x.UpdateDt, updateDt)
                    .SetProperty(x => x.UpdateUser, updateUser), cancellationToken)
                .ConfigureAwait(false);

            return affected == 1;
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
    /// 동의 기록을 등록한다.
    /// </summary>
    public async Task<bool> AddConsentsAsync(IEnumerable<Consent> models,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _context.Consents.AddRangeAsync(models, cancellationToken).ConfigureAwait(false);

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
    /// 살아 있는 휴대폰 인증번호를 PK로 불러온다(수정 대상)
    /// </summary>
    public async Task<PhoneVerification?> GetPhoneVerificationAsync(int phoneVerificationId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _context.PhoneVerifications
                .FirstOrDefaultAsync(x => x.PhoneVerificationId == phoneVerificationId
                                          && x.DeleteYn == false, cancellationToken)
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
    /// 휴대폰 인증번호 등록
    /// </summary>
    public async Task<bool> AddPhoneVerificationAsync(PhoneVerification model,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _context.PhoneVerifications.AddAsync(model, cancellationToken).ConfigureAwait(false);

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
    /// 휴대폰 인증번호 수정
    /// </summary>
    public async Task<bool> UpdatePhoneVerificationAsync(PhoneVerification model,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _context.PhoneVerifications.Update(model);

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
    /// 살아 있는 차량 로그인 세션을 PK로 불러온다(수정 대상)
    /// </summary>
    public async Task<LoginSession?> GetLoginSessionAsync(int loginSessionId, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _context.LoginSessions
                .FirstOrDefaultAsync(x => x.LoginSessionId == loginSessionId
                                          && x.SessionType == "CAR"
                                          && x.DeleteYn == false, cancellationToken)
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
    /// 로그인 세션 수정
    /// </summary>
    public async Task<bool> UpdateLoginSessionAsync(LoginSession model, CancellationToken cancellationToken = default)
    {
        try
        {
            _context.LoginSessions.Update(model);

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
    /// 로그인 세션을 PK로 끝낸다(DELETE_YN = 1). PK로만 잠가 빈 범위에 갭 락을 남기지 않는다.
    /// </summary>
    public async Task EndLoginSessionsAsync(IReadOnlyCollection<int> loginSessionIds, DateTime thisTime, string updateUser,
        CancellationToken cancellationToken = default)
    {
        if (loginSessionIds.Count == 0)
            return;

        try
        {
            await _context.LoginSessions
                .Where(x => loginSessionIds.Contains(x.LoginSessionId)
                            && x.DeleteYn == false)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.DeleteYn, true)
                    .SetProperty(x => x.UpdateDt, thisTime)
                    .SetProperty(x => x.UpdateUser, updateUser), cancellationToken)
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
    /// 로그인 세션 등록. 저장 뒤 model의 로그인 세션 ID가 채워진다.
    /// </summary>
    public async Task<bool> AddLoginSessionAsync(LoginSession model, CancellationToken cancellationToken = default)
    {
        try
        {
            await _context.LoginSessions.AddAsync(model, cancellationToken).ConfigureAwait(false);

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
    /// 정리 대상 휴대폰 인증번호의 번호 해시·인증번호 해시를 비우고 삭제 표시한다. ID와 함께 대상 조건을 다시 걸어, 그 사이 바뀐 행은 건드리지 않는다. 바뀐 행 수를 돌려준다.
    /// </summary>
    public async Task<int> CleanupPhoneVerificationsAsync(IReadOnlyCollection<int> phoneVerificationIds,
        DateTime verifiedBefore, DateTime expiredBefore, DateTime thisTime, string updateUser,
        CancellationToken cancellationToken = default)
    {
        if (phoneVerificationIds.Count == 0)
            return 0;

        try
        {
            return await _context.PhoneVerifications
                .Where(x => phoneVerificationIds.Contains(x.PhoneVerificationId)
                            && x.DeleteYn == false
                            && ((x.VerifyDt != null && x.VerifyDt < verifiedBefore)
                                || (x.VerifyDt == null && x.ExpireDt < expiredBefore)))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.PhoneHash, string.Empty)
                    .SetProperty(x => x.CodeHash, string.Empty)
                    .SetProperty(x => x.DeleteYn, true)
                    .SetProperty(x => x.UpdateDt, thisTime)
                    .SetProperty(x => x.UpdateUser, updateUser), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested) // 요청 취소는 로그 없이 올려보낸다
        {
            _logService.LogMessage(ex.ToString());
#if DEBUG
            _logService.ConsoleWarning(ex.ToString());
#endif
            throw; // Service가 로그만 남기고 그 회차를 끝낸다
        }
    }
}
