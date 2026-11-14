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
[Flags]
public enum PickupLiveSignal
{
    /// <summary>신호 없음</summary>
    None = 0,
    /// <summary>계획 변경</summary>
    PlanChanged = 1,
    /// <summary>차 위치 변경</summary>
    VehicleMoved = 2,
    /// <summary>계획 종료</summary>
    PlanEnded = 4
}

/// <summary>
/// 서버 한 대의 메모리에서 계획별 차량 위치와 구독을 관리한다
/// </summary>
public interface IPickupLiveHub
{
    /// <summary>
    /// 최소 보고 간격을 충족하면 위치를 저장하고 구독에 알린다.
    /// parkedSince(DB의 정차 시각)가 있고 정차 표시가 비어 있으면 간격에 막혀도 표시를 채운다(서버 재시작으로 사라진 표시 복구)
    /// </summary>
    bool TrySetVehiclePosition(int pickupPlanId, VehiclePosition position, DateTime? parkedSince = null);

    /// <summary>
    /// 정차한 위치를 최소 보고 간격과 상관없이 저장하고 정차 표시를 남긴다. 신호는 보내지 않으므로 커밋 뒤 NotifyVehicleMoved로 알린다.
    /// 계획이 끝나거나 VehicleParkedMaxMin이 지날 때까지 오래됐다고 지우지 않는다
    /// </summary>
    void SetParkedVehiclePosition(int pickupPlanId, VehiclePosition position);

    /// <summary>차 위치 변경을 구독에 알린다</summary>
    void NotifyVehicleMoved(int pickupPlanId);

    /// <summary>마지막 차량 위치를 돌려준다. 없으면 null</summary>
    VehiclePosition? GetVehiclePosition(int pickupPlanId);

    /// <summary>계획 변경을 구독에 알린다</summary>
    void NotifyPlanChanged(int pickupPlanId);

    /// <summary>위치를 지우고 계획 종료를 구독에 알린다</summary>
    void NotifyPlanEnded(int pickupPlanId);

    /// <summary>계획을 구독한다. 상한은 대상(아이·차량)마다 세고, 넘으면 그 대상의 가장 오래된 구독을 완료한다</summary>
    PickupLiveSubscription Subscribe(int pickupPlanId, PickupLiveAudience audience);
}

/// <summary>
/// 구독 대상. 연결 상한을 대상마다 따로 세서 아이 화면 연결이 차량 연결을 밀어내지 않게 한다
/// </summary>
public enum PickupLiveAudience
{
    /// <summary>아이 휴대폰 웹(픽업 안내 실시간 연결)</summary>
    Child,
    /// <summary>차량 앱(픽업 링크 전달 상태 실시간 연결)</summary>
    Vehicle
}

/// <summary>
/// 계획별 신호 구독. Dispose를 여러 번 호출해도 안전하다
/// 신호는 종류별 플래그로 합쳐지므로 위치 신호가 계획 신호를 지우지 못한다
/// </summary>
public sealed class PickupLiveSubscription : IDisposable
{
    private Action? _dispose;
    private int _pending;
    private readonly Channel<bool> _wake = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
    {
        FullMode = BoundedChannelFullMode.DropWrite,
        SingleReader = true
    });

    internal PickupLiveSubscription(Action<PickupLiveSubscription> dispose)
    {
        this._dispose = () => dispose(this);
    }

    /// <summary>깨우기 채널. 완료되면 밀려났거나 해제된 구독이다</summary>
    public ChannelReader<bool> Wake => _wake.Reader;

    /// <summary>지금까지 쌓인 신호 플래그를 가져가고 비운다</summary>
    public PickupLiveSignal TakeSignals()
    {
        return (PickupLiveSignal)Interlocked.Exchange(ref _pending, 0);
    }

    internal void Post(PickupLiveSignal signal)
    {
        Interlocked.Or(ref _pending, (int)signal);
        _wake.Writer.TryWrite(true);
    }

    internal void Complete()
    {
        _wake.Writer.TryComplete();
    }

    /// <summary>허브에서 구독을 해제하고 채널을 완료한다</summary>
    public void Dispose()
    {
        Interlocked.Exchange(ref _dispose, null)?.Invoke();
    }
}

/// <summary>
/// 서버 한 대 기준의 픽업 메모리 허브. 계획별 잠금으로 위치와 구독의 동시 접근을 보호한다
/// 계획이 끝나면(NotifyPlanEnded) 위치와 정차 표시를 지운다. 정차 위치는 정차 커밋 전에 계획 행 잠금을 쥔 채 저장하고
/// 탑승·취소·만료도 같은 행을 먼저 잠그므로 종료 알림은 항상 정차 저장 뒤에 와서 끝난 계획에 정차 위치가 남지 않는다
/// 끝난 계획은 위치 보고가 404이므로 남은 위치도 일반 위치는 VehicleStaleSec + 60초 안에, 정차 위치는 정차 뒤 VehicleParkedMaxMin + 60초 안에 Sweep이 지운다(안전망)
/// </summary>
public sealed class PickupLiveHub : IPickupLiveHub, IDisposable
{
    private readonly ConcurrentDictionary<int, PlanState> _plans = new();
    private readonly PickupLiveOptions _options;
    private readonly System.Threading.Timer _timer;

    /// <summary>실시간 픽업 설정으로 허브를 만든다</summary>
    public PickupLiveHub(IOptions<PickupLiveOptions> options)
    {
        this._options = options.Value;
        this._timer = new System.Threading.Timer(_ =>
        {
            try
            {
                Sweep();
            }
            catch
            {
                // 정리 실패는 다음 회차에 다시 한다
            }
        }, null, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(60));
    }

    /// <summary>오래된 위치 정리 타이머를 해제한다</summary>
    public void Dispose()
    {
        _timer.Dispose();
    }

    private void Sweep()
    {
        foreach (var pair in _plans)
        {
            var state = pair.Value;
            lock (state)
            {
                // 정차한 위치는 VehicleStaleSec로 지우지 않고 정차 뒤 VehicleParkedMaxMin이 지나야 지운다(무한히 남지 않게 하는 상한)
                if (state.Position is { } position
                    && (state.ParkedDt is { } parkedDt
                        ? (DateTime.Now - parkedDt).TotalMinutes > _options.VehicleParkedMaxMin
                        : (DateTime.Now - position.ReceivedDt).TotalSeconds > _options.VehicleStaleSec))
                {
                    state.Position = null;
                    state.ParkedDt = null;
                }
                RemoveIfEmpty(pair.Key, state);
            }
        }
    }

    /// <summary>최소 보고 간격을 충족하면 위치를 저장하고 VehicleMoved를 알린다</summary>
    public bool TrySetVehiclePosition(int pickupPlanId, VehiclePosition position, DateTime? parkedSince = null)
    {
        while (true)
        {
            var state = _plans.GetOrAdd(pickupPlanId, _ => new PlanState());
            lock (state)
            {
                // 잠금을 기다리는 동안 제거된 항목이면 현재 사전 항목으로 다시 시도한다
                if (!IsCurrent(pickupPlanId, state))
                    continue;
                // 서버를 다시 띄우면 메모리의 정차 표시가 사라진다. DB의 정차 시각으로 복구해 보고가 끊겨도 상한(VehicleParkedMaxMin)까지 남긴다.
                // 표시 복구는 간격 검사보다 먼저 한다(거절된 보고에서도 같은 값을 쓰므로 해롭지 않고, 복구가 한 보고 늦어지지 않는다)
                if (parkedSince is not null && state.ParkedDt is null)
                    state.ParkedDt = parkedSince;
                if (state.Position is { } previous
                    && (position.ReceivedDt - previous.ReceivedDt).TotalSeconds < _options.PositionMinIntervalSec)
                    return false;

                state.Position = position;
                Publish(state, PickupLiveSignal.VehicleMoved);
                return true;
            }
        }
    }

    /// <summary>정차한 위치를 간격 제한 없이 저장하고 정차 표시를 남긴다. 신호는 보내지 않는다</summary>
    public void SetParkedVehiclePosition(int pickupPlanId, VehiclePosition position)
    {
        while (true)
        {
            var state = _plans.GetOrAdd(pickupPlanId, _ => new PlanState());
            lock (state)
            {
                if (!IsCurrent(pickupPlanId, state))
                    continue;
                state.Position = position;
                state.ParkedDt = position.ReceivedDt;
                return;
            }
        }
    }

    /// <summary>구독에 VehicleMoved를 알린다</summary>
    public void NotifyVehicleMoved(int pickupPlanId)
    {
        Notify(pickupPlanId, PickupLiveSignal.VehicleMoved);
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

    /// <summary>계획을 구독하고 그 대상의 상한을 넘으면 그 대상의 가장 오래된 구독을 완료한다</summary>
    public PickupLiveSubscription Subscribe(int pickupPlanId, PickupLiveAudience audience)
    {
        while (true)
        {
            var state = _plans.GetOrAdd(pickupPlanId, _ => new PlanState());
            lock (state)
            {
                if (!IsCurrent(pickupPlanId, state))
                    continue;
                var subscriptions = state.For(audience);
                var subscription = new PickupLiveSubscription(sub =>
                {
                    lock (state)
                    {
                        subscriptions.Remove(sub);
                        sub.Complete();
                        RemoveIfEmpty(pickupPlanId, state);
                    }
                });
                subscriptions.Add(subscription);
                if (subscriptions.Count > _options.MaxStreamsPerPlan)
                {
                    subscriptions[0].Complete();
                    subscriptions.RemoveAt(0);
                }
                return subscription;
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
                {
                    state.Position = null;
                    state.ParkedDt = null;
                }
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
        if (state.Position is null && state.ChildSubscriptions.Count == 0 && state.VehicleSubscriptions.Count == 0
            && IsCurrent(pickupPlanId, state))
            _plans.TryRemove(pickupPlanId, out _);
    }

    // 신호는 두 대상 모두에 보낸다. 무엇을 다시 읽을지는 각 스트림이 정한다
    private static void Publish(PlanState state, PickupLiveSignal signal)
    {
        foreach (var subscription in state.ChildSubscriptions)
            subscription.Post(signal);
        foreach (var subscription in state.VehicleSubscriptions)
            subscription.Post(signal);
    }

    private sealed class PlanState
    {
        public VehiclePosition? Position { get; set; }
        // 정차로 저장한 시각. 있으면 Sweep이 VehicleStaleSec 대신 VehicleParkedMaxMin으로 위치를 지운다
        public DateTime? ParkedDt { get; set; }
        public List<PickupLiveSubscription> ChildSubscriptions { get; } = new();
        public List<PickupLiveSubscription> VehicleSubscriptions { get; } = new();

        public List<PickupLiveSubscription> For(PickupLiveAudience audience) =>
            audience == PickupLiveAudience.Vehicle ? VehicleSubscriptions : ChildSubscriptions;
    }
}
