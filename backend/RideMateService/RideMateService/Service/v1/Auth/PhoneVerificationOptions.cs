namespace RideMateService.Service.v1.Auth;

/// <summary>
/// 휴대폰 인증번호 설정. 설정 절 "PhoneVerification".
/// </summary>
public class PhoneVerificationOptions
{
    /// <summary>
    /// 인증번호 행 정리 주기 작업의 간격(분)
    /// </summary>
    public int CleanupIntervalMin { get; set; } = 10;

    /// <summary>
    /// MVP 테스트용 고정 인증번호(숫자 6자리). 값이 있으면 모든 번호·용도에 이 번호를 발급하고 문자를 보내지 않는다. 비우면 난수를 만들어 문자로 보낸다
    /// </summary>
    public string? FixedCode { get; set; }
}
