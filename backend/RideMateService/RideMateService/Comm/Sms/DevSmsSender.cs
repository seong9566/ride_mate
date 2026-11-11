using RideMateService.Comm.Logger;

namespace RideMateService.Comm.Sms;

/// <summary>
/// 실제 문자를 발송하지 않는 개발용 문자 발송 구현
/// </summary>
public class DevSmsSender : ISmsSender
{
    private readonly IHostEnvironment _environment;
    private readonly ILogService _logService;

    public DevSmsSender(IHostEnvironment environment, ILogService logService)
    {
        this._environment = environment;
        this._logService = logService;
    }

    /// <summary>
    /// Development 환경에서는 가린 번호와 메시지를 콘솔에 남기고, 그 밖의 환경에서는 미설정 사실만 기록한다.
    /// </summary>
    public Task SendAsync(string phoneDigits, string message, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_environment.IsDevelopment())
        {
            var maskedPhone = new string('*', Math.Max(0, phoneDigits.Length - 4)) + phoneDigits[^Math.Min(4, phoneDigits.Length)..];
            _logService.ConsoleMessage($"[DEV SMS] {maskedPhone} {message}");
        }
        else
        {
            _logService.LogMessage("SMS sender is not configured");
        }

        return Task.CompletedTask;
    }
}
