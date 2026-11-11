namespace RideMateService.Comm.Sms;

/// <summary>
/// 문자 메시지 발송 서비스
/// </summary>
public interface ISmsSender
{
    /// <summary>
    /// 휴대폰 번호로 문자 메시지를 발송한다.
    /// </summary>
    Task SendAsync(string phoneDigits, string message, CancellationToken cancellationToken = default);
}
