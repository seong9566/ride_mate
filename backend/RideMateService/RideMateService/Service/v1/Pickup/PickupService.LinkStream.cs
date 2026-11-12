using System.Runtime.CompilerServices;
using System.Security.Claims;
using Newtonsoft.Json;
using RideMateService.Comm;
using RideMateService.Comm.Dto.v1.Pickup.Request;
using RideMateService.Comm.Dto.v1.Pickup.Response;

namespace RideMateService.Service.v1.Pickup;

public partial class PickupService
{
    private const string LinkStreamEventStatus = "linkStatus";
    private const string LinkStreamEventClosed = "closed";

    /// <summary>
    /// 차량 픽업 링크 전달 상태 실시간 연결을 판정한다. 401·400·404는 DetailPickupLinkStatus와 같다
    /// </summary>
    public async Task<ResponseModel<PickupLinkStreamTicket>> OpenPickupLinkStreamService(PickupLinkStatusQueryDto dto,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (!int.TryParse(user?.FindFirstValue("sub"), out var userId)
                || !long.TryParse(user?.FindFirstValue("exp"), out var exp))
                return new ResponseModel<PickupLinkStreamTicket> { message = "잘못된 요청입니다.", data = null, code = 401 };

            if (dto?.pickupPlanSeq is null)
                return new ResponseModel<PickupLinkStreamTicket> { message = "잘못된 요청입니다.", data = null, code = 400 };

            var result = await LoadPickupLinkStatusAsync(dto.pickupPlanSeq.Value, userId, cancellationToken)
                .ConfigureAwait(false);
            if (result is null)
                return new ResponseModel<PickupLinkStreamTicket> { message = "잘못된 요청입니다.", data = null, code = 404 };

            return new ResponseModel<PickupLinkStreamTicket>
            {
                message = "요청이 정상 처리되었습니다.",
                data = new PickupLinkStreamTicket(dto.pickupPlanSeq.Value, userId,
                    DateTimeOffset.FromUnixTimeSeconds(exp).LocalDateTime),
                code = 200
            };
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested) // 요청 취소는 그대로 올려보낸다
        {
            var message = $"vehicle link stream open failed: plan={dto?.pickupPlanSeq?.ToString() ?? "unknown"}\n{ex}";
            _logService.LogMessage(message);
#if DEBUG
            _logService.ConsoleWarning(message);
#endif
            return new ResponseModel<PickupLinkStreamTicket> { message = "서버에서 요청을 처리하지 못하였습니다.", data = null, code = 500 };
        }
    }

    /// <summary>차량 픽업 링크 전달 상태 실시간 이벤트를 읽는다. 오류는 기록하고 스트림을 끝낸다</summary>
    public async IAsyncEnumerable<SseEvent> ReadPickupLinkStreamAsync(PickupLinkStreamTicket ticket,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // 반복자의 구독·대기·조회·비교 오류를 MoveNextAsync 경계에서 처리한다(아이 스트림과 같은 구조)
        var reader = ReadPickupLinkEventsAsync(ticket, cancellationToken).GetAsyncEnumerator(cancellationToken);
        try
        {
            while (true)
            {
                bool moved;
                try
                {
                    moved = await reader.MoveNextAsync().ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    moved = false;
                }
                catch (Exception ex)
                {
                    LogPickupLinkStreamFailure(ticket.PickupPlanId, ex);
                    moved = false;
                }
                if (!moved)
                    yield break;
                yield return reader.Current;
            }
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
                LogPickupLinkStreamFailure(ticket.PickupPlanId, ex);
            }
        }
    }

    private void LogPickupLinkStreamFailure(int pickupPlanId, Exception ex)
    {
        var message = $"vehicle link stream failed: plan={pickupPlanId}\n{ex}";
        _logService.LogMessage(message);
#if DEBUG
        _logService.ConsoleWarning(message);
#endif
    }

    private async IAsyncEnumerable<SseEvent> ReadPickupLinkEventsAsync(PickupLinkStreamTicket ticket,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var connectedDt = DateTime.Now;
        // 구독을 먼저 건 뒤 읽어, 읽는 사이에 생긴 변경 신호를 놓치지 않는다
        using var sub = _liveHub.Subscribe(ticket.PickupPlanId, PickupLiveAudience.Vehicle);
        var initial = await LoadPickupLinkStatusAsync(ticket.PickupPlanId, ticket.UserId, cancellationToken)
            .ConfigureAwait(false);
        if (initial is null)
        {
            yield return new SseEvent(LinkStreamEventClosed, null);
            yield break;
        }
        yield return new SseEvent(LinkStreamEventStatus, initial.Value.Dto);

        var last = JsonConvert.SerializeObject(initial.Value.Dto);
        var expireDt = initial.Value.ExpireDt;
        var soonAt = LinkSoonAt(initial.Value.Dto);
        var deadline = connectedDt.AddMinutes(_liveOptions.StreamMaxLifetimeMin);
        if (ticket.TokenExpiresAt < deadline)
            deadline = ticket.TokenExpiresAt;
        var nextHeartbeat = connectedDt.AddSeconds(_liveOptions.HeartbeatSec);
        while (!cancellationToken.IsCancellationRequested)
        {
            var wake = nextHeartbeat < deadline ? nextHeartbeat : deadline;
            if (expireDt < wake)
                wake = expireDt;
            if (soonAt is { } soon && soon < wake)
                wake = soon;
            if (!await WaitForChildPickupSignalAsync(sub, wake, cancellationToken).ConfigureAwait(false))
                yield break;

            while (sub.Wake.TryRead(out _))
            {
            }
            // 차량 스트림은 차 위치(VehicleMoved)를 보내지 않는다
            var signals = sub.TakeSignals();
            var now = DateTime.Now;
            if (now >= deadline)
                yield break;
            var heartbeat = now >= nextHeartbeat;
            // 하트비트마다도 다시 계산한다. 결과 없이 30초가 지난 발송(SENDING→FAILED)처럼 시간만으로 바뀌는 상태를 잡는다
            if (signals.HasFlag(PickupLiveSignal.PlanChanged) || signals.HasFlag(PickupLiveSignal.PlanEnded)
                || heartbeat || now >= expireDt || soonAt is { } leaveSoon && now >= leaveSoon)
            {
                var next = await LoadPickupLinkStatusAsync(ticket.PickupPlanId, ticket.UserId, cancellationToken)
                    .ConfigureAwait(false);
                if (next is null)
                {
                    yield return new SseEvent(LinkStreamEventClosed, null);
                    yield break;
                }
                var json = JsonConvert.SerializeObject(next.Value.Dto);
                if (json != last)
                {
                    yield return new SseEvent(LinkStreamEventStatus, next.Value.Dto);
                    last = json;
                }
                expireDt = next.Value.ExpireDt;
                soonAt = LinkSoonAt(next.Value.Dto);
            }
            if (heartbeat)
            {
                yield return new SseEvent(null, null);
                nextHeartbeat = nextHeartbeat.AddSeconds(_liveOptions.HeartbeatSec);
            }
        }
    }

    // 출발 시각이 정해져 아직 멀면(SCHEDULED) SOON으로 바뀌는 시각. 그 밖에는 null
    private DateTime? LinkSoonAt(DetailPickupLinkStatusDto dto)
    {
        return dto.leaveStatus == ChildLeaveState.StatusScheduled && dto.plannedLeaveDt is { } planned
            ? planned.AddMinutes(-_linkOptions.LeaveSoonMin)
            : null;
    }
}
