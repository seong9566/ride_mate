using Microsoft.EntityFrameworkCore;

namespace RideMateService.Repository.v1.User;

public partial class UserRepository
{
    /// <summary>
    /// 집 주소·좌표와 수정 감사 컬럼만 바꾼다. 좌표를 null로 주면 NULL로 바꾼다. 바뀐 행이 없으면 false를 돌려준다.
    /// </summary>
    public async Task<bool> UpdateHomeAsync(int userId, string address, decimal? lat, decimal? lng,
        DateTime updateDt, string updateUser, CancellationToken cancellationToken = default)
    {
        try
        {
            var affected = await _context.UserInfos
                .Where(x => x.UserId == userId
                            && x.DeleteYn == false)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.DestAddress, address)
                    .SetProperty(x => x.DestLat, lat)
                    .SetProperty(x => x.DestLng, lng)
                    .SetProperty(x => x.UpdateDt, updateDt)
                    .SetProperty(x => x.UpdateUser, updateUser), cancellationToken)
                .ConfigureAwait(false);

            return affected > 0;
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
    /// 차 정보 중 null이 아닌 값과 수정 감사 컬럼만 바꾼다. null인 인자는 그 칸을 바꾸지 않는다. 바뀐 행이 없으면 false를 돌려준다.
    /// </summary>
    public async Task<bool> UpdateVehicleAsync(int userId, string? carModel, string? carColor, string? carNoLast4,
        DateTime updateDt, string updateUser, CancellationToken cancellationToken = default)
    {
        try
        {
            // 값이 온 칸만 SET에 넣는다 — 안 온 칸을 옛 값으로 덮어쓰지 않는다
            var affected = await _context.UserInfos
                .Where(x => x.UserId == userId
                            && x.DeleteYn == false)
                .ExecuteUpdateAsync(setters =>
                {
                    if (carModel is not null)
                        setters.SetProperty(x => x.CarModel, carModel);
                    if (carColor is not null)
                        setters.SetProperty(x => x.CarColor, carColor);
                    if (carNoLast4 is not null)
                        setters.SetProperty(x => x.CarNoLast4, carNoLast4);

                    setters.SetProperty(x => x.UpdateDt, updateDt);
                    setters.SetProperty(x => x.UpdateUser, updateUser);
                }, cancellationToken)
                .ConfigureAwait(false);

            return affected > 0;
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
    /// 초기 설정 완료 시각과 수정 감사 컬럼만 바꾼다. 완료 시각이 비어 있는 계정만 바꾸므로, 이미 채워져 있으면 false를 돌려준다.
    /// </summary>
    public async Task<bool> UpdateSetupDoneAsync(int userId, DateTime setupDoneDt, string updateUser,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // 완료 시각이 이미 있으면 덮어쓰지 않는다 — 다시 눌러도 처음 시각이 그대로다
            var affected = await _context.UserInfos
                .Where(x => x.UserId == userId
                            && x.DeleteYn == false
                            && x.SetupDoneDt == null)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.SetupDoneDt, setupDoneDt)
                    .SetProperty(x => x.UpdateDt, setupDoneDt)
                    .SetProperty(x => x.UpdateUser, updateUser), cancellationToken)
                .ConfigureAwait(false);

            return affected > 0;
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
    /// 자동 발송 여부와 수정 감사 컬럼만 바꾼다. 바뀐 행이 없으면 false를 돌려준다.
    /// </summary>
    public async Task<bool> UpdateAutoSendAsync(int userId, bool autoSendYn, DateTime updateDt, string updateUser,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var affected = await _context.UserInfos
                .Where(x => x.UserId == userId
                            && x.DeleteYn == false)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.AutoSendYn, autoSendYn)
                    .SetProperty(x => x.UpdateDt, updateDt)
                    .SetProperty(x => x.UpdateUser, updateUser), cancellationToken)
                .ConfigureAwait(false);

            return affected > 0;
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
