using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;
using RideMateService.Commons.DBModels;

namespace RideMateService.Repository.v1.Pickup;

public partial class PickupRepository
{
    /// <summary>
    /// 오늘 계획의 픽업 세션을 불러온다. 끝난 계획의 세션도 돌려준다
    /// </summary>
    public async Task<PickupSession?> GetPickupSessionAsync(int pickupPlanId, CancellationToken cancellationToken = default)
    {
        try
        {
            // 픽업 계열 테이블은 DELETE_YN이 "끝나서 익명화됨"이라 조건을 붙이지 않는다(schema.md)
            return await _context.PickupSessions
                .FirstOrDefaultAsync(x => x.PickupPlanId == pickupPlanId, cancellationToken)
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
    /// 픽업 세션 등록
    /// </summary>
    public async Task<bool> AddPickupSessionAsync(PickupSession model, CancellationToken cancellationToken = default)
    {
        try
        {
            await _context.PickupSessions.AddAsync(model, cancellationToken).ConfigureAwait(false);

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
    /// 픽업 이벤트를 PK로 불러온다(수정 대상). 끝난 계획의 이벤트도 돌려준다
    /// </summary>
    public async Task<PickupEvent?> GetPickupEventAsync(int pickupEventId, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _context.PickupEvents
                .FirstOrDefaultAsync(x => x.PickupEventId == pickupEventId, cancellationToken)
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
    /// 픽업 이벤트 등록. 저장 뒤 model의 픽업 이벤트 ID가 채워진다
    /// </summary>
    public async Task<bool> AddPickupEventAsync(PickupEvent model, CancellationToken cancellationToken = default)
    {
        try
        {
            await _context.PickupEvents.AddAsync(model, cancellationToken).ConfigureAwait(false);

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
    /// 픽업 이벤트 수정
    /// </summary>
    public async Task<bool> UpdatePickupEventAsync(PickupEvent model, CancellationToken cancellationToken = default)
    {
        try
        {
            _context.PickupEvents.Update(model);

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
    /// 오늘 계획 등록. 저장 뒤 model의 오늘 계획 ID가 채워진다.
    /// 같은 요일·날짜의 계획이 이미 있으면 MySQL 중복 키 예외(1062, DbUpdateException 안쪽)를 로그 없이 올려보낸다
    /// </summary>
    public async Task<bool> AddPickupPlanAsync(PickupPlan model, CancellationToken cancellationToken = default)
    {
        try
        {
            await _context.PickupPlans.AddAsync(model, cancellationToken).ConfigureAwait(false);

            return await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false) > 0;
        }
        // 요청 취소는 로그 없이 올려보낸다. 중복 키(1062)는 동시 생성 경합이라 에러로 기록하지 않고 Service에 맡긴다
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
    /// 오늘 계획을 PK로 불러온다(수정 대상, 추적). 끝난 계획도 돌려준다
    /// </summary>
    public async Task<PickupPlan?> GetPickupPlanEntityAsync(int pickupPlanId, CancellationToken cancellationToken = default)
    {
        try
        {
            // 픽업 계열 테이블은 DELETE_YN이 "끝나서 익명화됨"이라 조건을 붙이지 않는다(schema.md)
            return await _context.PickupPlans
                .FirstOrDefaultAsync(x => x.PickupPlanId == pickupPlanId, cancellationToken)
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
    /// 오늘 계획 수정
    /// </summary>
    public async Task<bool> UpdatePickupPlanAsync(PickupPlan model, CancellationToken cancellationToken = default)
    {
        try
        {
            _context.PickupPlans.Update(model);

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
