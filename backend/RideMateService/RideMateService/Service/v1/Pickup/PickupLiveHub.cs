using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.Options;

namespace RideMateService.Service.v1.Pickup;

/// <summary>
/// 서버 메모리에만 두는 차량의 마지막 위치와 내비 남은 시간
/// </summary>
/// <param name="Lat">차 위도</param>
/// <param name="Lng">차 경도</param>
/// <param name="EtaSec">픽업 장소까지 내비 남은 시간(초)</param>
/// <param name="ReceivedDt">서버 보고 수신 시각</param>
public sealed record VehiclePosition(decimal Lat, decimal Lng, int? EtaSec, DateTime ReceivedDt);

/// <summary>
/// 픽업 실시간 구독 신호
/// </summary>
public enum PickupLiveSignal
{
    /// <summary>계획 변경</summary>
    PlanChanged,
    /// <summary>차 위치 변경</summary>
    VehicleMoved,
    /// <summary>계획 종료</summary>
    PlanEnded
}

/// <summary>
/// 서버 한 대의 메모리에서 계획별 차량 위치와 구독을 관리한다
/// </summary>
public interface IPickupLiveHub
{
    /// <summary>최소 보고 간격을 충족하면 위치를 저장하고 구독에 알린다</summary>
    bool TrySetVehiclePosition(int pickupPlanId, VehiclePosition position);

    /// <summary>마지막 차량 위치를 돌려준다. 없으면 null</summary>
    VehiclePosition? GetVehiclePosition(int pickupPlanId);

    /// <summary>계획 변경을 구독에 알린다</summary>
    void NotifyPlanChanged(int pickupPlanId);

    /// <summary>위치를 지우고 계획 종료를 구독에 알린다</summary>
    void NotifyPlanEnded(int pickupPlanId);

    /// <summary>계획을 구독한다. 상한을 넘으면 가장 오래된 구독을 완료한다</summary>
    PickupLiveSubscription Subscribe(int pickupPlanId);
}

/// <summary>
/// 계획별 신호 구독. Dispose를 여러 번 호출해도 안전하다
/// </summary>
public sealed class PickupLiveSubscription : IDisposable
{
    private Action? _dispose;

    internal PickupLiveSubscription(ChannelReader<PickupLiveSignal> signals, Action dispose)
    {
        Signals = signals;
        this._dispose = dispose;
    }

    /// <summary>계획 변경 신호를 읽는 채널</summary>
    public ChannelReader<PickupLiveSignal> Signals { get; }

    /// <summary>허브에서 구독을 해제하고 채널을 완료한다</summary>
    public void Dispose()
    {
        Interlocked.Exchange(ref _dispose, null)?.Invoke();
    }
}

/// <summary>
/// 서버 한 대 기준의 픽업 메모리 허브. 계획별 잠금으로 위치와 구독의 동시 접근을 보호한다
/// </summary>
public sealed class PickupLiveHub : IPickupLiveHub
{
    private readonly ConcurrentDictionary<int, PlanState> _plans = new();
    private readonly PickupLiveOptions _options;

    /// <summary>실시간 픽업 설정으로 허브를 만든다</summary>
    public PickupLiveHub(IOptions<PickupLiveOptions> options)
    {
        this._options = options.Value;
    }

    /// <summary>최소 보고 간격을 충족하면 위치를 저장하고 VehicleMoved를 알린다</summary>
    public bool TrySetVehiclePosition(int pickupPlanId, VehiclePosition position)
    {
        while (true)
        {
            var state = _plans.GetOrAdd(pickupPlanId, _ => new PlanState());
            lock (state)
            {
                // 잠금을 기다리는 동안 제거된 항목이면 현재 사전 항목으로 다시 시도한다
                if (!IsCurrent(pickupPlanId, state))
                    continue;
                if (state.Position is { } previous
                    && (position.ReceivedDt - previous.ReceivedDt).TotalSeconds < _options.PositionMinIntervalSec)
                    return false;

                state.Position = position;
                Publish(state, PickupLiveSignal.VehicleMoved);
                return true;
            }
        }
    }

    /// <summary>계획의 마지막 차량 위치를 돌려준다</summary>
    public VehiclePosition? GetVehiclePosition(int pickupPlanId)
    {
        while (_plans.TryGetValue(pickupPlanId, out var state))
        {
            lock (state)
            {
                if (!IsCurrent(pickupPlanId, state))
                    continue;
                return state.Position;
            }
        }
        return null;
    }

    /// <summary>구독에 PlanChanged를 알린다</summary>
    public void NotifyPlanChanged(int pickupPlanId)
    {
        Notify(pickupPlanId, PickupLiveSignal.PlanChanged);
    }

    /// <summary>위치를 지우고 PlanEnded를 알린 뒤 비어 있는 계획을 제거한다</summary>
    public void NotifyPlanEnded(int pickupPlanId)
    {
        Notify(pickupPlanId, PickupLiveSignal.PlanEnded);
    }

    /// <summary>계획을 구독하고 상한을 넘으면 가장 오래된 구독을 완료한다</summary>
    public PickupLiveSubscription Subscribe(int pickupPlanId)
    {
        while (true)
        {
            var state = _plans.GetOrAdd(pickupPlanId, _ => new PlanState());
            lock (state)
            {
                if (!IsCurrent(pickupPlanId, state))
                    continue;
                var channel = Channel.CreateBounded<PickupLiveSignal>(new BoundedChannelOptions(16)
                {
                    FullMode = BoundedChannelFullMode.DropOldest,
                    SingleReader = true
                });
                state.Subscriptions.Add(channel);
                if (state.Subscriptions.Count > _options.MaxStreamsPerPlan)
                {
                    state.Subscriptions[0].Writer.Complete();
                    state.Subscriptions.RemoveAt(0);
                }
                return new PickupLiveSubscription(channel.Reader, () =>
                {
                    lock (state)
                    {
                        state.Subscriptions.Remove(channel);
                        channel.Writer.TryComplete();
                        RemoveIfEmpty(pickupPlanId, state);
                    }
                });
            }
        }
    }

    private void Notify(int pickupPlanId, PickupLiveSignal signal)
    {
        while (_plans.TryGetValue(pickupPlanId, out var state))
        {
            lock (state)
            {
                if (!IsCurrent(pickupPlanId, state))
                    continue;
                if (signal == PickupLiveSignal.PlanEnded)
                    state.Position = null;
                Publish(state, signal);
                RemoveIfEmpty(pickupPlanId, state);
                return;
            }
        }
    }

    private bool IsCurrent(int pickupPlanId, PlanState state)
    {
        return _plans.TryGetValue(pickupPlanId, out var current) && ReferenceEquals(current, state);
    }

    // 호출자는 반드시 해당 계획의 잠금을 보유한다
    private void RemoveIfEmpty(int pickupPlanId, PlanState state)
    {
        if (state.Position is null && state.Subscriptions.Count == 0 && IsCurrent(pickupPlanId, state))
            _plans.TryRemove(pickupPlanId, out _);
    }

    private static void Publish(PlanState state, PickupLiveSignal signal)
    {
        foreach (var channel in state.Subscriptions)
            channel.Writer.TryWrite(signal);
    }

    private sealed class PlanState
    {
        public VehiclePosition? Position { get; set; }
        public List<Channel<PickupLiveSignal>> Subscriptions { get; } = new();
    }
}
