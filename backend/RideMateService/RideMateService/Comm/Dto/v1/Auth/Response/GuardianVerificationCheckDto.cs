namespace RideMateService.Comm.Dto.v1.Auth.Response;

/// <summary>
/// 보호자 인증번호 확인 결과
/// </summary>
public class GuardianVerificationCheckDto
{
    /// <summary>
    /// 입력이 잠기기 전까지 남은 횟수(인증번호가 틀렸을 때만)
    /// </summary>
    public int? remainingAttempts { get; set; }

    /// <summary>
    /// 입력 잠금이 풀리기까지 남은 시간(초, 잠겼을 때만)
    /// </summary>
    public int? retryAfter { get; set; }
}
