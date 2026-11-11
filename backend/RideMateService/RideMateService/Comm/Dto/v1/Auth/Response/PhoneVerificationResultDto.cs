namespace RideMateService.Comm.Dto.v1.Auth.Response;

/// <summary>
/// 로그인 인증번호 발송 결과
/// </summary>
public class PhoneVerificationResultDto
{
    /// <summary>
    /// 인증번호 유효 시간(초)
    /// </summary>
    public int expiresIn { get; set; }

    /// <summary>
    /// 다시 발송할 수 있을 때까지 남은 시간(초)
    /// </summary>
    public int retryAfter { get; set; }
}
