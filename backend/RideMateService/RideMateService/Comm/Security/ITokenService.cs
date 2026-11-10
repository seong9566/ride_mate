namespace RideMateService.Comm.Security;

/// <summary>
/// 로그인 토큰(access JWT, refresh 난수)을 만든다.
/// </summary>
public interface ITokenService
{
    /// <summary>
    /// access 토큰 유효 시간(초)
    /// </summary>
    const int AccessTokenSeconds = 3600;

    /// <summary>
    /// refresh 토큰 유효 기간(일)
    /// </summary>
    const int RefreshTokenDays = 90;

    /// <summary>
    /// refresh 교체 직전 토큰을 허용하는 시간(분)
    /// </summary>
    const int RefreshGraceMinutes = 10;

    /// <summary>
    /// HS256 access 토큰을 만든다. 클레임은 sub(사용자), sid(로그인 세션), did(기기)이고 만료는 thisTime + 3600초다.
    /// </summary>
    string CreateAccessToken(int userId, int loginSessionId, string deviceId, DateTime thisTime);

    /// <summary>
    /// 32바이트 난수를 base64url로 만든다.
    /// </summary>
    string CreateRefreshToken();
}
