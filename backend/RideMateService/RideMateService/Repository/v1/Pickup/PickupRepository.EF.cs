using Microsoft.EntityFrameworkCore;
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
    /// 조회 시각이 저장된 시각보다 나중일 때 주차장 빈자리 캐시를 갱신한다
    /// </summary>
    public async Task<int> UpdateAvailableSpacesAsync(int parkingLotId, short availableSpaces, DateTime checkedDt,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _context.ParkingLots
                .Where(x => x.ParkingLotId == parkingLotId
                            && (x.AvailableUpdateDt == null || x.AvailableUpdateDt < checkedDt))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.AvailableSpaces, availableSpaces)
                    .SetProperty(x => x.AvailableUpdateDt, checkedDt), cancellationToken)
                .ConfigureAwait(false);
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
