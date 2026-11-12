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
}
