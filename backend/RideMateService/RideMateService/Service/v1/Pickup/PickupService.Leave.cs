using Microsoft.EntityFrameworkCore.Storage;
using RideMateService.Comm;
using RideMateService.Comm.Dto.v1.Pickup.Request;
using RideMateService.Comm.Dto.v1.Pickup.Response;
using RideMateService.Commons.DBModels;
using RideMateService.Repository.v1.Pickup;

namespace RideMateService.Service.v1.Pickup;

public partial class PickupService
{
    private const string ChildAuditUser = "CHILD";
    private const string ReasonLinkClosed = "LINK_CLOSED";
    private const string ReasonAlreadyLeaving = "ALREADY_LEAVING";

    /// <summary>
    /// 아이가 지금 출발하거나 출발 예정 시각을 설정·변경한다
    /// </summary>
    public async Task<ResponseModel<ChildLeaveDto>> AddChildLeaveService(string? token, AddChildLeaveDto dto,
        CancellationToken cancellationToken = default)
    {
        int? pickupPlanId = null;
        try
        {
            var leaveDt = NormalizeLeaveDt(dto?.leaveDt);

            // 지금 나가요는 되돌릴 수 없으므로 leaveNow: true로만 받는다. 빈 값·키 오타가 지금 나가요로 기록되지 않게 둘 중 하나만 허용한다
            var leaveNow = dto?.leaveNow == true;
            if (dto?.leaveNow == false || leaveNow == leaveDt.HasValue)
                return LeaveBadRequest();

            if (!PickupLinkToken.IsWellFormed(token))
                return LeaveNotFound();

            var session = await _pickupRepository.GetLeaveSessionAsync(
                PickupLinkToken.HashToken(_hashService, token!), cancellationToken).ConfigureAwait(false);
            if (session is null)
                return LeaveNotFound();

            pickupPlanId = session.PickupPlanId;
            var transaction = await _context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await using var transactionScope = transaction.ConfigureAwait(false);

            var plan = await _pickupRepository.LockPickupPlanAsync(session.PickupPlanId,
                transaction.GetDbTransaction(), cancellationToken).ConfigureAwait(false);
            var now = NowToSecond();
            // CloseDt는 잠그기 전에 읽은 값이지만, 계획을 끝내는 트랜잭션이 STATUS와 세션 CLOSE_DT를 함께 바꾸므로 잠근 뒤의 STATUS 검사가 그 경우를 잡는다
            if (plan is null || ClosedPlanStatuses.Contains(plan.Status) || plan.ExpireDt <= now
                || session.CloseDt is not null)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return LeaveConflict(ReasonLinkClosed,
                    new ChildLeaveSummary(ChildLeaveState.StatusNone, null, null));
            }

            var events = await _pickupRepository.GetLeaveEventsAsync(session.PickupPlanId,
                transaction.GetDbTransaction(), cancellationToken).ConfigureAwait(false);
            var summary = ChildLeaveState.Summarize(events, now, _linkOptions.LeaveSoonMin);

            if (leaveDt is null)
            {
                if (summary.Status == ChildLeaveState.StatusLeaving)
                {
                    await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    return LeaveSuccess(summary);
                }

                var pickupEvent = NewChildLeaveEvent(session.PickupPlanId,
                    ChildLeaveState.EventChildLeaving, now, null);
                if (!await _pickupRepository.AddPickupEventAsync(pickupEvent, cancellationToken).ConfigureAwait(false))
                {
                    await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    return LeaveServerError();
                }

                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                _context.ChangeTracker.Clear();
                AddLeaveEvent(events, pickupEvent);
                return LeaveSuccess(ChildLeaveState.Summarize(events, now, _linkOptions.LeaveSoonMin));
            }

            if (summary.Status == ChildLeaveState.StatusLeaving)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return LeaveConflict(ReasonAlreadyLeaving, summary);
            }

            // 같은 시각으로 다시 정하면 범위와 상관없이 바꿀 것이 없다(설계 4.3)
            if (summary.PlannedLeaveDt == leaveDt.Value)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return LeaveSuccess(summary);
            }

            var floorMinute = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0);
            if (leaveDt.Value < floorMinute.AddMinutes(-1)
                || leaveDt.Value > now.AddMinutes(_linkOptions.LeaveMaxAheadMin)
                || leaveDt.Value >= plan.ExpireDt)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return LeaveBadRequest();
            }

            var detail = ChildLeaveState.BuildDetail(leaveDt.Value);
            var scheduledEvent = NewChildLeaveEvent(session.PickupPlanId,
                ChildLeaveState.EventLeaveScheduled, now, detail);
            if (!await _pickupRepository.AddPickupEventAsync(scheduledEvent, cancellationToken).ConfigureAwait(false))
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return LeaveServerError();
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            _context.ChangeTracker.Clear();
            AddLeaveEvent(events, scheduledEvent);
            return LeaveSuccess(ChildLeaveState.Summarize(events, now, _linkOptions.LeaveSoonMin));
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested) // 요청 취소는 그대로 올려보낸다
        {
            _logService.LogMessage($"child leave add failed: plan={pickupPlanId?.ToString() ?? "unknown"}\n{ex}");
#if DEBUG
            _logService.ConsoleWarning($"child leave add failed: plan={pickupPlanId?.ToString() ?? "unknown"}\n{ex}");
#endif
            return LeaveServerError();
        }
    }

    /// <summary>
    /// 아이의 출발 예정 시각을 취소한다
    /// </summary>
    public async Task<ResponseModel<ChildLeaveDto>> DeleteChildLeaveService(string? token,
        CancellationToken cancellationToken = default)
    {
        int? pickupPlanId = null;
        try
        {
            if (!PickupLinkToken.IsWellFormed(token))
                return LeaveNotFound();

            var session = await _pickupRepository.GetLeaveSessionAsync(
                PickupLinkToken.HashToken(_hashService, token!), cancellationToken).ConfigureAwait(false);
            if (session is null)
                return LeaveNotFound();

            pickupPlanId = session.PickupPlanId;
            var transaction = await _context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await using var transactionScope = transaction.ConfigureAwait(false);

            var plan = await _pickupRepository.LockPickupPlanAsync(session.PickupPlanId,
                transaction.GetDbTransaction(), cancellationToken).ConfigureAwait(false);
            var now = NowToSecond();
            // CloseDt는 잠그기 전에 읽은 값이지만, 계획을 끝내는 트랜잭션이 STATUS와 세션 CLOSE_DT를 함께 바꾸므로 잠근 뒤의 STATUS 검사가 그 경우를 잡는다
            if (plan is null || ClosedPlanStatuses.Contains(plan.Status) || plan.ExpireDt <= now
                || session.CloseDt is not null)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return LeaveConflict(ReasonLinkClosed,
                    new ChildLeaveSummary(ChildLeaveState.StatusNone, null, null));
            }

            var events = await _pickupRepository.GetLeaveEventsAsync(session.PickupPlanId,
                transaction.GetDbTransaction(), cancellationToken).ConfigureAwait(false);
            var summary = ChildLeaveState.Summarize(events, now, _linkOptions.LeaveSoonMin);

            if (summary.Status == ChildLeaveState.StatusLeaving)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return LeaveConflict(ReasonAlreadyLeaving, summary);
            }

            if (summary.Status == ChildLeaveState.StatusNone)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return LeaveSuccess(summary);
            }

            var pickupEvent = NewChildLeaveEvent(session.PickupPlanId,
                ChildLeaveState.EventLeaveCanceled, now, null);
            if (!await _pickupRepository.AddPickupEventAsync(pickupEvent, cancellationToken).ConfigureAwait(false))
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return LeaveServerError();
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            _context.ChangeTracker.Clear();
            AddLeaveEvent(events, pickupEvent);
            return LeaveSuccess(ChildLeaveState.Summarize(events, now, _linkOptions.LeaveSoonMin));
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested) // 요청 취소는 그대로 올려보낸다
        {
            _logService.LogMessage($"child leave delete failed: plan={pickupPlanId?.ToString() ?? "unknown"}\n{ex}");
#if DEBUG
            _logService.ConsoleWarning($"child leave delete failed: plan={pickupPlanId?.ToString() ?? "unknown"}\n{ex}");
#endif
            return LeaveServerError();
        }
    }

    // DATETIME 컬럼은 소수 초를 반올림해 저장한다. 응답의 leavingDt가 DB에서 다시 읽은 값과 같도록 초 단위로 자르고 오프셋 없이 쓴다
    private static DateTime NowToSecond()
    {
        var now = DateTime.Now;
        return new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second, DateTimeKind.Unspecified);
    }

    private static DateTime? NormalizeLeaveDt(DateTime? leaveDt)
    {
        if (leaveDt is not { } value)
            return null;

        if (value.Kind is DateTimeKind.Utc or DateTimeKind.Local)
            value = DateTime.SpecifyKind(value.ToLocalTime(), DateTimeKind.Unspecified);

        return new DateTime(value.Year, value.Month, value.Day, value.Hour, value.Minute, 0);
    }

    private static PickupEvent NewChildLeaveEvent(int pickupPlanId, string eventType, DateTime now, string? detail)
    {
        return new PickupEvent
        {
            PickupPlanId = pickupPlanId,
            EventType = eventType,
            EventDt = now,
            ClientEventId = null,
            Detail = detail,
            CreateDt = now,
            CreateUser = ChildAuditUser,
            UpdateDt = now,
            UpdateUser = ChildAuditUser,
            DeleteYn = false
        };
    }

    private static void AddLeaveEvent(List<LinkEventRow> events, PickupEvent pickupEvent)
    {
        events.Add(new LinkEventRow
        {
            PickupEventId = pickupEvent.PickupEventId,
            EventType = pickupEvent.EventType,
            EventDt = pickupEvent.EventDt,
            ClientEventId = null,
            Detail = pickupEvent.Detail,
            CreateDt = pickupEvent.CreateDt
        });
    }

    private static ResponseModel<ChildLeaveDto> LeaveSuccess(ChildLeaveSummary summary)
    {
        return new ResponseModel<ChildLeaveDto>
        {
            message = "요청이 정상 처리되었습니다.",
            data = ToChildLeaveDto(summary, null),
            code = 200
        };
    }

    private static ResponseModel<ChildLeaveDto> LeaveBadRequest()
    {
        return new ResponseModel<ChildLeaveDto> { message = "잘못된 요청입니다.", data = null, code = 400 };
    }

    private static ResponseModel<ChildLeaveDto> LeaveNotFound()
    {
        return new ResponseModel<ChildLeaveDto> { message = "잘못된 요청입니다.", data = null, code = 404 };
    }

    private static ResponseModel<ChildLeaveDto> LeaveConflict(string reason, ChildLeaveSummary summary)
    {
        return new ResponseModel<ChildLeaveDto>
        {
            message = "잘못된 요청입니다.",
            data = ToChildLeaveDto(summary, reason),
            code = 409
        };
    }

    private static ResponseModel<ChildLeaveDto> LeaveServerError()
    {
        return new ResponseModel<ChildLeaveDto>
        {
            message = "서버에서 요청을 처리하지 못하였습니다.",
            data = null,
            code = 500
        };
    }

    private static ChildLeaveDto ToChildLeaveDto(ChildLeaveSummary summary, string? reason)
    {
        return new ChildLeaveDto
        {
            reason = reason,
            leaveStatus = summary.Status,
            plannedLeaveDt = summary.PlannedLeaveDt,
            leavingDt = summary.LeavingDt
        };
    }
}
