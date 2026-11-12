using Microsoft.Extensions.Options;
using RideMateService.Comm.Logger;

namespace RideMateService.Service.v1.Pickup;

/// <summary>
/// 만료 주기 작업. 시작하자마자 한 회차를 돌고(재시작 동안 밀린 만료 처리), 이후 PickupPlan:ExpireIntervalSec마다 돈다.
/// 회차마다 스코프를 새로 만들어 Scoped DbContext를 쓰는 IPickupService를 꺼낸다. 한 회차의 오류는 로그만 남기고 다음 회차로 간다
/// </summary>
public sealed class PickupPlanExpiryWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogService _logService;
    private readonly TimeSpan _interval;

    public PickupPlanExpiryWorker(IServiceScopeFactory scopeFactory, ILogService logService,
        IOptions<PickupPlanOptions> planOptions)
    {
        this._scopeFactory = scopeFactory;
        this._logService = logService;
        this._interval = TimeSpan.FromSeconds(planOptions.Value.ExpireIntervalSec);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logService.LogMessage($"Pickup plan expiry worker: ON (interval {_interval.TotalSeconds:0}s)");

        using var timer = new PeriodicTimer(_interval);
        try
        {
            do
            {
                await RunOnceAsync(stoppingToken).ConfigureAwait(false);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) // 서버 종료
        {
        }
    }

    // 한 회차. 예외를 밖으로 내보내면 호스트가 멈추므로 여기서 모두 받는다(종료 취소만 올려보낸다)
    private async Task RunOnceAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var pickupService = scope.ServiceProvider.GetRequiredService<IPickupService>();
            var expired = await pickupService.ExpirePickupPlansService(stoppingToken).ConfigureAwait(false);
            if (expired > 0)
                _logService.LogMessage($"pickup plan expired: count={expired}");
        }
        catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
        {
            _logService.LogMessage(ex.ToString());
#if DEBUG
            _logService.ConsoleWarning(ex.ToString());
#endif
        }
    }
}
