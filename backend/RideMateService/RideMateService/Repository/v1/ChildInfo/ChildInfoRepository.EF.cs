using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;
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
    /// 아이 등록. 저장 뒤 model의 아이 ID가 채워진다. 같은 번호의 살아 있는 아이가 있으면 MySQL 중복 키 예외(1062)를 로그 없이 올려보낸다.
    /// </summary>
    public async Task<bool> AddChildAsync(Child model, CancellationToken cancellationToken = default)
    {
        try
        {
            await _context.Children.AddAsync(model, cancellationToken).ConfigureAwait(false);

            return await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false) > 0;
        }
        // 요청 취소는 로그 없이 올려보낸다. 중복 키(1062)는 정상 업무 결과(103)라 에러로 기록하지 않고 Service에 맡긴다
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested && !IsDuplicateKey(ex))
        {
            _logService.LogMessage(ex.ToString());
#if DEBUG
            _logService.ConsoleWarning(ex.ToString());
#endif
            throw; // Service가 500으로 바꾼다
        }
    }

    /// <summary>
    /// 살아 있는 아이의 이름·암호화한 번호·번호 해시·발송 채널을 바꾼다. 같은 번호의 다른 살아 있는 아이가 있으면 MySQL 중복 키 예외(1062)를 로그 없이 올려보낸다. 바뀐 행이 없으면 false다.
    /// </summary>
    public async Task<bool> UpdateChildAsync(int childId, string childName, byte[] phoneEnc, string? phoneHash,
        string sendChannel, DateTime updateDt, string updateUser, CancellationToken cancellationToken = default)
    {
        try
        {
            var affected = await _context.Children
                .Where(x => x.ChildId == childId
                            && x.DeleteYn == false)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.ChildName, childName)
                    .SetProperty(x => x.PhoneEnc, phoneEnc)
                    .SetProperty(x => x.PhoneHash, phoneHash)
                    .SetProperty(x => x.SendChannel, sendChannel)
                    .SetProperty(x => x.UpdateDt, updateDt)
                    .SetProperty(x => x.UpdateUser, updateUser), cancellationToken)
                .ConfigureAwait(false);

            return affected > 0;
        }
        // 요청 취소는 로그 없이 올려보낸다. 중복 키(1062)는 정상 업무 결과(103)라 에러로 기록하지 않고 Service에 맡긴다
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested && !IsDuplicateKey(ex))
        {
            _logService.LogMessage(ex.ToString());
#if DEBUG
            _logService.ConsoleWarning(ex.ToString());
#endif
            throw; // Service가 500으로 바꾼다
        }
    }

    private static bool IsDuplicateKey(Exception? ex)
    {
        for (; ex is not null; ex = ex.InnerException)
        {
            if (ex is MySqlException { Number: 1062 })
                return true;
        }

        return false;
    }
}
