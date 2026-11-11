using Newtonsoft.Json;

namespace RideMateService.Comm.Dto.v1.Auth.Response;

/// <summary>
/// 로그인 세션 결과. 성공이면 토큰 칸만, 실패면 오류 칸만 채운다
/// </summary>
public class LoginSessionResultDto
{
    /// <summary>
    /// access 토큰(JWT)
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string? accessToken { get; set; }

    /// <summary>
    /// 토큰 종류(Bearer)
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string? tokenType { get; set; }

    /// <summary>
    /// access 토큰 유효 시간(초)
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public int? expiresIn { get; set; }

    /// <summary>
    /// 새 refresh 토큰
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string? refreshToken { get; set; }

    /// <summary>
    /// 로그인 세션 ID
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public int? sessionId { get; set; }

    /// <summary>
    /// 초기 설정이 필요한지(운전자 이름이 비었거나 살아 있는 아이가 없으면 true)
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public bool? setupRequired { get; set; }

    /// <summary>
    /// 오류 코드
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string? error { get; set; }

    /// <summary>
    /// 남은 인증번호 시도 횟수
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public int? remainingAttempts { get; set; }

    /// <summary>
    /// 잠금이 풀리기까지 남은 시간(초)
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public int? retryAfter { get; set; }
}
