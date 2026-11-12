using System.Data;
using Dapper;

namespace RideMateService.Repository.v1.Pickup;

public partial class PickupRepository
{
    /// <summary>
    /// 토큰 해시로 픽업 세션의 계획 ID와 종료 시각을 잠금 없이 조회한다. 세션이 없으면 null
    /// </summary>
    public async Task<LeaveSessionRow?> GetLeaveSessionAsync(string tokenHash,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var dp = new DynamicParameters();
            dp.Add("tokenHash", tokenHash);

            // 픽업 계열 테이블은 DELETE_YN이 "끝나서 익명화됨"이라 조건을 붙이지 않는다(schema.md).
            var sql = @"
                SELECT s.PICKUP_PLAN_ID AS PickupPlanId,
                       s.CLOSE_DT AS CloseDt
                FROM PICKUP_SESSION AS s
                WHERE s.TOKEN_HASH = @tokenHash";

            return await _dapper.QueryFirstOrDefaultAsync<LeaveSessionRow>(
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
    /// 아이 출발 관련 이벤트(CHILD_LEAVING·LEAVE_SCHEDULED·LEAVE_CANCELED)를 PICKUP_EVENT_ID 순으로 조회한다
    /// </summary>
    public async Task<List<LinkEventRow>> GetLeaveEventsAsync(int pickupPlanId, IDbTransaction? transaction,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var dp = new DynamicParameters();
            dp.Add("pickupPlanId", pickupPlanId);

            // 픽업 계열 테이블은 DELETE_YN이 "끝나서 익명화됨"이라 조건을 붙이지 않는다(schema.md).
            // 드라이버가 CHAR(36)을 Guid로 읽어 string에 매핑하지 못하므로 CLIENT_EVENT_ID는 문자열로 바꿔 읽는다
            var sql = @"
                SELECT e.PICKUP_EVENT_ID AS PickupEventId,
                       e.EVENT_TYPE AS EventType,
                       e.EVENT_DT AS EventDt,
                       CAST(e.CLIENT_EVENT_ID AS CHAR) AS ClientEventId,
                       e.DETAIL AS Detail,
                       e.CREATE_DT AS CreateDt
                FROM PICKUP_EVENT AS e
                WHERE e.PICKUP_PLAN_ID = @pickupPlanId
                  AND e.EVENT_TYPE IN ('CHILD_LEAVING', 'LEAVE_SCHEDULED', 'LEAVE_CANCELED')
                ORDER BY e.PICKUP_EVENT_ID";

            var rows = await _dapper.QueryAsync<LinkEventRow>(
                new CommandDefinition(sql, dp, transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
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
}
