using System.Runtime.CompilerServices;
using RideMateService.Comm;
using RideMateService.Comm.Dto.v1.Pickup.Response;

namespace RideMateService.Service.v1.Pickup;

public partial class PickupService
{
    /// <summary>아이 픽업 실시간 연결을 판정한다</summary>
    public async Task<ResponseModel<ChildPickupStreamTicket>> OpenChildPickupStreamService(string? token,
        CancellationToken cancellationToken = default)
    {
        int? pickupPlanId = null;
        try
        {
            if (!PickupLinkToken.IsWellFormed(token))
                return new ResponseModel<ChildPickupStreamTicket> { code = 404, data = null, message = "잘못된 요청입니다." };

            var tokenHash = PickupLinkToken.HashToken(_hashService, token!);
            var result = await LoadChildPickupAsync(tokenHash, cancellationToken).ConfigureAwait(false);
            if (result is null)
                return new ResponseModel<ChildPickupStreamTicket> { code = 404, data = null, message = "잘못된 요청입니다." };

            pickupPlanId = result.Value.PickupPlanId;
            return new ResponseModel<ChildPickupStreamTicket>
            {
                code = 200,
                message = "요청이 정상 처리되었습니다.",
                data = new ChildPickupStreamTicket(pickupPlanId.Value, tokenHash)
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            pickupPlanId ??= ex.Data["ChildPickupPlanId"] as int?;
            var message = $"child pickup stream open failed: plan={pickupPlanId?.ToString() ?? "unknown"}\n{ex}";
            _logService.LogMessage(message);
#if DEBUG
            _logService.ConsoleWarning(message);
#endif
            return new ResponseModel<ChildPickupStreamTicket>
            {
                code = 500, data = null, message = "서버에서 요청을 처리하지 못하였습니다."
            };
        }
    }

    /// <summary>아이 픽업 실시간 이벤트를 읽는다. 오류는 기록하고 스트림을 끝낸다</summary>
    public async IAsyncEnumerable<SseEvent> ReadChildPickupStreamAsync(
        ChildPickupStreamTicket ticket, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // 실제 반복자의 구독·대기·조회·비교 오류를 MoveNextAsync 경계에서 처리한다.
        var reader = ReadChildPickupEventsAsync(ticket, cancellationToken).GetAsyncEnumerator(cancellationToken);
        try
        {
            while (await TryReadChildPickupEventAsync(reader, ticket.PickupPlanId, cancellationToken).ConfigureAwait(false))
                yield return reader.Current;
        }
        finally
        {
            try
            {
                await reader.DisposeAsync().ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                LogChildPickupStreamFailure(ticket.PickupPlanId, ex);
            }
        }
    }

    private async Task<bool> TryReadChildPickupEventAsync(IAsyncEnumerator<SseEvent> reader,
        int pickupPlanId, CancellationToken cancellationToken)
    {
        try
        {
            return await reader.MoveNextAsync().ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception ex)
        {
            LogChildPickupStreamFailure(pickupPlanId, ex);
            return false;
        }
    }

    private void LogChildPickupStreamFailure(int pickupPlanId, Exception ex)
    {
        var message = $"child pickup stream failed: plan={pickupPlanId}\n{ex}";
        _logService.LogMessage(message);
#if DEBUG
        _logService.ConsoleWarning(message);
#endif
    }

    private async IAsyncEnumerable<SseEvent> ReadChildPickupEventsAsync(
        ChildPickupStreamTicket ticket, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var connectedDt = DateTime.Now;
        using var sub = _liveHub.Subscribe(ticket.PickupPlanId, PickupLiveAudience.Child);
        var initial = await LoadChildPickupAsync(ticket.TokenHash, cancellationToken).ConfigureAwait(false);
        if (initial is null)
            yield break;
        cancellationToken.ThrowIfCancellationRequested();
        yield return SnapshotEvent(initial.Value.Dto, null);
        if (initial.Value.Dto.linkStatus != LinkStatusActive)
            yield break;

        var last = initial.Value.Dto;
        var expireDt = initial.Value.ExpireDt;
        DateTime? soonAt = last.leave?.leaveStatus == "SCHEDULED" && last.leave.plannedLeaveDt is { } p
            ? p.AddMinutes(-_linkOptions.LeaveSoonMin) : null;
        var deadline = connectedDt.AddMinutes(_liveOptions.StreamMaxLifetimeMin);
        var nextHeartbeat = connectedDt.AddSeconds(_liveOptions.HeartbeatSec);
        var lastVehicleHadPosition = last.vehicle is not null;
        while (!cancellationToken.IsCancellationRequested)
        {
            var wake = nextHeartbeat < deadline ? nextHeartbeat : deadline;
            if (expireDt is { } expires && expires < wake)
                wake = expires;
            if (soonAt is { } soon && soon < wake)
                wake = soon;
            // 보낸 차 위치가 오래되는 시각에도 깨어나 위치 칸이 빈 vehicle을 보낸다(하트비트까지 기다리면 최대 15초 늦다).
            // 받은 시각은 초 단위라 1초를 더해야 깨어난 시각에 확실히 오래된 위치로 판정된다
            var staleAt = lastVehicleHadPosition && _liveHub.GetVehiclePosition(ticket.PickupPlanId) is { } held
                ? held.ReceivedDt.AddSeconds(_liveOptions.VehicleStaleSec + 1) : (DateTime?)null;
            if (staleAt is { } staleWake && staleWake < wake)
                wake = staleWake;
            if (!await WaitForChildPickupSignalAsync(sub, wake, cancellationToken).ConfigureAwait(false))
                yield break;

            while (sub.Wake.TryRead(out _))
            {
            }
            var signals = sub.TakeSignals();
            var reload = signals.HasFlag(PickupLiveSignal.PlanChanged) || signals.HasFlag(PickupLiveSignal.PlanEnded);
            var moved = signals.HasFlag(PickupLiveSignal.VehicleMoved);
            var now = DateTime.Now;
            if (reload || expireDt is { } expiration && now >= expiration
                || soonAt is { } leaveSoon && now >= leaveSoon)
            {
                var next = await LoadChildPickupAsync(ticket.TokenHash, cancellationToken).ConfigureAwait(false);
                if (next is null)
                    yield break;
                var change = ChildPickupLiveState.Compare(last, next.Value.Dto);
                if (change is not null || next.Value.Dto.linkStatus != LinkStatusActive)
                {
                    yield return SnapshotEvent(next.Value.Dto, change);
                    lastVehicleHadPosition = next.Value.Dto.vehicle is not null;
                }
                last = next.Value.Dto;
                expireDt = next.Value.ExpireDt;
                soonAt = last.leave?.leaveStatus == "SCHEDULED" && last.leave.plannedLeaveDt is { } planned
                    ? planned.AddMinutes(-_linkOptions.LeaveSoonMin) : null;
                if (last.linkStatus != LinkStatusActive)
                    yield break;
                now = DateTime.Now;
            }
            if (moved && _liveOptions.VehiclePositionEnabled)
            {
                var position = VehicleEvent(ticket.PickupPlanId, last.slotDt);
                yield return new SseEvent("vehicle", position);
                lastVehicleHadPosition = position.lat is not null;
            }
            if (now >= deadline)
                yield break;
            if (now >= nextHeartbeat)
            {
                yield return new SseEvent(null, null);
                nextHeartbeat = nextHeartbeat.AddSeconds(_liveOptions.HeartbeatSec);
            }
            if (lastVehicleHadPosition && (staleAt is null || now >= staleAt))
            {
                var position = VehicleEvent(ticket.PickupPlanId, last.slotDt);
                if (position.lat is null)
                {
                    yield return new SseEvent("vehicle", position);
                    lastVehicleHadPosition = false;
                }
            }
        }
    }

    private static async Task<bool> WaitForChildPickupSignalAsync(PickupLiveSubscription sub, DateTime wake,
        CancellationToken cancellationToken)
    {
        using var waitCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        try
        {
            var remaining = wake - DateTime.Now;
            var signal = sub.Wake.WaitToReadAsync(waitCancellation.Token).AsTask();
            var timer = Task.Delay(remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero, waitCancellation.Token);
            await Task.WhenAny(signal, timer).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return !sub.Wake.Completion.IsCompleted && (!signal.IsCompleted || await signal.ConfigureAwait(false));
        }
        finally
        {
            // 타이머가 먼저 끝난 경우에도 채널 대기가 다음 반복까지 남지 않게 한다.
            waitCancellation.Cancel();
        }
    }

    private ChildPickupVehicleEventDto VehicleEvent(int pickupPlanId, DateTime? slotDt)
    {
        // 응답 시각은 다른 시각 필드처럼 오프셋 없는 KST 초 단위로 내보낸다(DateTime.Now는 Kind가 Local이라 오프셋이 붙는다)
        var now = NowToSecond();
        var position = ResolveVehicle(pickupPlanId, slotDt, now);
        return new ChildPickupVehicleEventDto
        {
            lat = position.Vehicle?.lat,
            lng = position.Vehicle?.lng,
            reportedDt = position.Vehicle?.reportedDt,
            arrivalDt = position.ArrivalDt,
            arrivalSource = position.Vehicle is null ? ArrivalSourceSlot : position.ArrivalSource,
            serverDt = now
        };
    }

    // ChildPickupDto에 필드를 더하면 여기 복사에도 더한다(조회 API와 스트림 스냅샷의 모양이 같아야 한다)
    private static SseEvent SnapshotEvent(ChildPickupDto dto, string? change)
    {
        return new SseEvent("snapshot", new ChildPickupSnapshotEventDto
        {
            linkStatus = dto.linkStatus,
            serverDt = dto.serverDt,
            driverName = dto.driverName,
            car = dto.car,
            academy = dto.academy,
            pickup = dto.pickup,
            previousPickup = dto.previousPickup,
            walkPath = dto.walkPath,
            planStatus = dto.planStatus,
            stopAtSpot = dto.stopAtSpot,
            slotDt = dto.slotDt,
            stopDt = dto.stopDt,
            arrivalDt = dto.arrivalDt,
            arrivalSource = dto.arrivalSource,
            vehicle = dto.vehicle,
            leave = dto.leave,
            change = change
        });
    }
}
