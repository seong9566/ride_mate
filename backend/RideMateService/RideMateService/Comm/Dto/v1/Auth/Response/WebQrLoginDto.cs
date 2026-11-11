using Newtonsoft.Json;

namespace RideMateService.Comm.Dto.v1.Auth.Response;

/// <summary>
/// 휴대폰 웹 QR 로그인 결과. 값이 없는 칸은 JSON에서 뺀다
/// </summary>
public class WebQrLoginDto
{
    /// <summary>
    /// QR 로그인 상태
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string? qrState { get; set; }

    /// <summary>
    /// QR 로그인 취소 사유
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string? cancelReason { get; set; }

    /// <summary>
    /// 휴대폰 웹 로그인 여부
    /// </summary>
    public bool signedIn { get; set; }

    /// <summary>
    /// 처음 가입에 이름·약관이 필요한지
    /// </summary>
    public bool signupRequired { get; set; }

    /// <summary>
    /// 운전자 이름
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string? userName { get; set; }

    /// <summary>
    /// 가린 휴대폰 번호
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string? maskedPhone { get; set; }

    /// <summary>
    /// 차와 휴대폰에 함께 보이는 숫자 4자리
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string? matchCode { get; set; }

    /// <summary>
    /// QR 만료까지 남은 시간(초)
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public int? expiresIn { get; set; }

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

    /// <summary>
    /// Controller가 쿠키로만 내보낼 새 웹 세션 토큰
    /// </summary>
    [JsonIgnore]
    public string? webSessionToken { get; set; }
}
