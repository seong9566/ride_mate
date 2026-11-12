using Dapper;

namespace RideMateService.Repository.v1.Pickup;

public partial class PickupRepository
{
    /// <summary>
    /// 픽업 계획의 운전자 이름과 차량 정보를 조회한다. 운전자가 없으면 null
    /// </summary>
    public async Task<ChildPickupDriverRow?> GetChildPickupDriverAsync(int pickupPlanId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var dp = new DynamicParameters();
            dp.Add("pickupPlanId", pickupPlanId);

            // 픽업 계열 테이블은 DELETE_YN이 "끝나서 익명화됨"이라 조건을 붙이지 않는다(schema.md).
            var sql = @"
                SELECT u.USER_NAME AS UserName,
                       u.CAR_MODEL AS CarModel,
                       u.CAR_COLOR AS CarColor,
                       u.CAR_NO_LAST4 AS CarNoLast4
                FROM PICKUP_PLAN AS p
                INNER JOIN USER_INFO AS u ON u.USER_ID = p.USER_ID AND u.DELETE_YN = FALSE
                WHERE p.PICKUP_PLAN_ID = @pickupPlanId";

            return await _dapper.QueryFirstOrDefaultAsync<ChildPickupDriverRow>(
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

    /// <summary>
    /// 픽업 계획의 마지막 정차 이벤트 종류(STOPPED·WRONG_STOP)를 조회한다. 없으면 null
    /// </summary>
    public async Task<string?> GetLastStopEventTypeAsync(int pickupPlanId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var dp = new DynamicParameters();
            dp.Add("pickupPlanId", pickupPlanId);

            // 픽업 계열 테이블은 DELETE_YN이 "끝나서 익명화됨"이라 조건을 붙이지 않는다(schema.md).
            var sql = @"
                SELECT e.EVENT_TYPE
                FROM PICKUP_EVENT AS e
                WHERE e.PICKUP_PLAN_ID = @pickupPlanId
                  AND e.EVENT_TYPE IN ('STOPPED', 'WRONG_STOP')
                ORDER BY e.PICKUP_EVENT_ID DESC
                LIMIT 1";

            return await _dapper.QueryFirstOrDefaultAsync<string>(
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
