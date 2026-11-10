using Microsoft.EntityFrameworkCore;
using RideMateService.Commons.DBModels;

namespace RideMateService.Repository.v1.Auth;

public partial class AuthRepository
{
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
}
