namespace RideMateService.Comm.Sms;

/// <summary>
/// 문자 메시지 발송 서비스
/// </summary>
public interface ISmsSender
{
    /// <summary>
    /// 휴대폰 번호로 문자 메시지를 발송한다. 사업자 오류와 타임아웃은 Success = false로 돌려주고, 요청 취소만 예외로 올린다.
    /// </summary>
    Task<SmsSendResult> SendAsync(string phoneDigits, string message, CancellationToken cancellationToken = default);
}
