using Dapper;
using Microsoft.EntityFrameworkCore;
using RideMateService.Commons.DBModels;

namespace RideMateService.Repository.v1.Pickup;

public partial class PickupRepository
{
    /// <summary>
    /// 로그인한 부모의 살아 있는 아이의 살아 있는 시간표에서 학원과 학원가를 조회한다. 없거나 남의 시간표면 null
    /// </summary>
    public async Task<SpotRequestScheduleRow?> GetSpotRequestScheduleAsync(int userId, int timetableId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var dp = new DynamicParameters();
            dp.Add("userId", userId);
            dp.Add("timetableId", timetableId);

            var sql = @"
                SELECT a.ACADEMY_ID AS AcademyId,
                       a.AREA_ID AS AreaId
                FROM TIMETABLE AS t
                INNER JOIN CHILD AS c
                        ON c.CHILD_ID = t.CHILD_ID
                       AND c.USER_ID = @userId
                       AND c.DELETE_YN = FALSE
                INNER JOIN ACADEMY AS a
                        ON a.ACADEMY_ID = t.ACADEMY_ID
                       AND a.DELETE_YN = FALSE
                WHERE t.TIMETABLE_ID = @timetableId
                  AND t.DELETE_YN = FALSE";

            return await _dapper.QueryFirstOrDefaultAsync<SpotRequestScheduleRow>(
                new CommandDefinition(sql, dp, cancellationToken: cancellationToken)).ConfigureAwait(false);
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
    /// 픽업 장소 추가 요청 등록. 저장 뒤 model의 요청 ID가 채워진다
    /// </summary>
    public async Task<bool> AddPickupSpotRequestAsync(PickupSpotRequest model,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _context.PickupSpotRequests.AddAsync(model, cancellationToken).ConfigureAwait(false);

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
