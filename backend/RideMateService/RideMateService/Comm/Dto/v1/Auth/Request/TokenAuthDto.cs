namespace RideMateService.Comm.Dto.v1.Auth.Request;

/// <summary>
/// 토큰 발급 요청(form). OAuth 2.0 형식이라 form 필드 이름을 그대로 쓰고, 검증은 Service에서 한다
/// </summary>
public class TokenAuthDto
{
    /// <summary>
    /// 발급 방식(refresh_token, otp, urn:ietf:params:oauth:grant-type:device_code)
    /// </summary>
    public string? grant_type { get; set; }

    /// <summary>
    /// refresh 토큰 원문
    /// </summary>
    public string? refresh_token { get; set; }

    /// <summary>
    /// QR 로그인 발급 때 차량이 받은 device_code 원문
    /// </summary>
    public string? device_code { get; set; }

    /// <summary>
    /// 숫자로만 된 휴대폰 번호(otp 발급)
    /// </summary>
    public string? phone { get; set; }

    /// <summary>
    /// 6자리 인증번호(otp 발급)
    /// </summary>
    public string? code { get; set; }

    /// <summary>
    /// 차량 앱 설치 번호(otp 발급)
    /// </summary>
    public string? device_id { get; set; }
}
