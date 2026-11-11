using Newtonsoft.Json;

namespace RideMateService.Comm.Dto.v1.Auth.Response;

/// <summary>
/// 토큰 발급 실패 응답(OAuth 2.0 형식). 값이 없는 칸은 JSON에서 뺀다
/// </summary>
public class TokenAuthErrorDto
{
    /// <summary>
    /// 오류 코드(invalid_request, unsupported_grant_type, invalid_grant, otp_locked, otp_expired, otp_mismatch, signup_required 등)
    /// </summary>
    public string error { get; set; } = string.Empty;

    /// <summary>
    /// 오류 설명(영어 한 줄)
    /// </summary>
    public string error_description { get; set; } = string.Empty;

    /// <summary>
    /// 남은 인증번호 시도 횟수
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public int? remaining_attempts { get; set; }

    /// <summary>
    /// 잠금이 풀리기까지 남은 시간(초)
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public int? retry_after { get; set; }
}
