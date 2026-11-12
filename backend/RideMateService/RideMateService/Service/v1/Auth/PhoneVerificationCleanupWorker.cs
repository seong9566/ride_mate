using Microsoft.Extensions.Options;
using RideMateService.Comm.Logger;

namespace RideMateService.Service.v1.Auth;

/// <summary>
/// 인증번호 행 정리 주기 작업. 시작하자마자 한 회차를 돌고(재시작 동안 밀린 정리), 이후 PhoneVerification:CleanupIntervalMin마다 돈다.
/// 회차마다 스코프를 새로 만들어 Scoped DbContext를 쓰는 IAuthService를 꺼낸다. 한 회차의 오류는 로그만 남기고 다음 회차로 간다
/// </summary>
public sealed class PhoneVerificationCleanupWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogService _logService;
    private readonly TimeSpan _interval;

    public PhoneVerificationCleanupWorker(IServiceScopeFactory scopeFactory, ILogService logService,
        IOptions<PhoneVerificationOptions> phoneVerificationOptions)
    {
        this._scopeFactory = scopeFactory;
        this._logService = logService;
        this._interval = TimeSpan.FromMinutes(phoneVerificationOptions.Value.CleanupIntervalMin);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logService.LogMessage($"Phone verification cleanup worker: ON (interval {_interval.TotalMinutes:0}m)");

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
            var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
            var cleaned = await authService.CleanupPhoneVerificationsService(stoppingToken).ConfigureAwait(false);
            if (cleaned > 0)
                _logService.LogMessage($"phone verification cleaned: count={cleaned}");
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
