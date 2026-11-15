using RideMateService.Comm.Logger;

namespace RideMateService.Comm.Sms;

/// <summary>
/// 실제 문자를 발송하지 않는 개발용 문자 발송 구현. Development 환경에서만 등록된다
/// </summary>
public class DevSmsSender : ISmsSender
{
    private readonly ILogService _logService;

    public DevSmsSender(ILogService logService)
    {
        this._logService = logService;
    }

    /// <summary>
    /// 가린 번호와 메시지를 콘솔에 남긴다. 항상 Success = true, ResultCode = DEV를 돌려준다.
    /// </summary>
    public Task<SmsSendResult> SendAsync(string phoneDigits, string message, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var maskedPhone = new string('*', Math.Max(0, phoneDigits.Length - 4)) + phoneDigits[^Math.Min(4, phoneDigits.Length)..];
        _logService.ConsoleMessage($"[DEV SMS] {maskedPhone} {message}");

        return Task.FromResult(new SmsSendResult(true, "DEV"));
    }
}
