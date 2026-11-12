using Dapper;

namespace RideMateService.Repository.v1.Pickup;

public partial class PickupRepository
{
    /// <summary>
    /// 위치 보고 대상 계획의 소유자·상태·만료 시각을 잠금 없이 조회한다
    /// </summary>
    public async Task<PositionPlanRow?> GetPositionPlanAsync(int pickupPlanId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var dp = new DynamicParameters();
            dp.Add("pickupPlanId", pickupPlanId);

            var sql = @"
                SELECT p.USER_ID AS UserId,
                       p.STATUS AS Status,
                       p.EXPIRE_DT AS ExpireDt
                FROM PICKUP_PLAN AS p
                WHERE p.PICKUP_PLAN_ID = @pickupPlanId";

            return await _dapper.QueryFirstOrDefaultAsync<PositionPlanRow>(
                new CommandDefinition(sql, dp, cancellationToken: cancellationToken)).ConfigureAwait(false);
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
