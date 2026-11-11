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
}
