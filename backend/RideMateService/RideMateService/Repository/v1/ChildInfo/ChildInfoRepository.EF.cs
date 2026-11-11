using Microsoft.EntityFrameworkCore;
using RideMateService.Commons.DBModels;

namespace RideMateService.Repository.v1.ChildInfo;

public partial class ChildInfoRepository
{
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
    /// 아이 등록. 저장 뒤 model의 아이 ID가 채워진다. 같은 번호의 살아 있는 아이가 있으면 MySQL 중복 키 예외(1062)를 올려보낸다.
    /// </summary>
    public async Task<bool> AddChildAsync(Child model, CancellationToken cancellationToken = default)
    {
        try
        {
            await _context.Children.AddAsync(model, cancellationToken).ConfigureAwait(false);

            return await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false) > 0;
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested) // 요청 취소는 로그 없이 올려보낸다
        {
            _logService.LogMessage(ex.ToString());
#if DEBUG
            _logService.ConsoleWarning(ex.ToString());
#endif
            throw; // Service가 500(중복 키는 103)으로 바꾼다
        }
    }
}
