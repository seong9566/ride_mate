namespace RideMateService.Comm.Security;

/// <summary>
/// 로그인 토큰(JWT) 발급 설정. 설정 절 "Jwt". SigningKey는 base64 문자열이다.
/// </summary>
public class JwtOptions
{
    /// <summary>
    /// 토큰 발급자(iss)
    /// </summary>
    public string Issuer { get; set; } = string.Empty;

    /// <summary>
    /// 토큰 수신자(aud)
    /// </summary>
    public string Audience { get; set; } = string.Empty;

    /// <summary>
    /// HS256 서명 키(base64, 32바이트 이상)
    /// </summary>
    public string SigningKey { get; set; } = string.Empty;
}

/// <summary>
/// 해시·암호화 키 설정. 설정 절 "Crypto". 값은 모두 base64 문자열이다.
/// </summary>
public class CryptoOptions
{
    /// <summary>
    /// 전화번호 AES-256-GCM 암호화 키(base64, 정확히 32바이트)
    /// </summary>
    public string PhoneEncKey { get; set; } = string.Empty;

    /// <summary>
    /// HMAC-SHA256 해시 키(base64, 32바이트 이상)
    /// </summary>
    public string HashKey { get; set; } = string.Empty;
}

/// <summary>
/// QR 로그인 설정. 설정 절 "QrLogin".
/// </summary>
public class QrLoginOptions
{
    /// <summary>
    /// QR을 연 휴대폰이 접속할 절대 http(s) 주소
    /// </summary>
    public string VerificationUri { get; set; } = string.Empty;
}
