namespace RideMateService.Comm.Dto.v1.Auth.Response;

/// <summary>
/// 보호자 인증번호 발송 결과
/// </summary>
public class GuardianVerificationResultDto
{
    /// <summary>
    /// 남은 유효 시간(초)
    /// </summary>
    public int expiresIn { get; set; }

    /// <summary>
    /// 다시 받을 수 있을 때까지(초)
    /// </summary>
    public int retryAfter { get; set; }

    /// <summary>
    /// 가린 번호
    /// </summary>
    public string? phoneMasked { get; set; }
}
