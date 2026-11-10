namespace RideMateService.Comm.Security;

/// <summary>
/// 로그인 토큰(access JWT, refresh)을 만든다.
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
    /// refresh 교체 직전 토큰을 허용하는 시간(시간 단위)
    /// </summary>
    const int RefreshGraceHours = 24;

    /// <summary>
    /// HS256 access 토큰을 만든다. 클레임은 sub(사용자), sid(로그인 세션), did(기기)이고 만료는 thisTime + 3600초다.
    /// </summary>
    string CreateAccessToken(int userId, int loginSessionId, string deviceId, DateTime thisTime);

    /// <summary>
    /// 32바이트 난수를 base64url로 만든다. 로그인으로 처음 발급할 때 쓴다(카드 T08). 교체에는 DeriveNextRefreshToken을 쓴다.
    /// </summary>
    string CreateRefreshToken();

    /// <summary>
    /// 교체할 refresh 값을 만든다. base64url(HMAC-SHA256(Crypto:HashKey, "refresh-next:" + 세션ID + ":" + 지금 낸 값))이다.
    /// 같은 입력이면 늘 같은 값이 나오므로, 직전 값으로 다시 보낸 요청도 처음 요청과 같은 새 값을 받는다.
    /// 원문은 어디에도 저장하지 않는다.
    /// </summary>
    string DeriveNextRefreshToken(int loginSessionId, string currentRefreshToken);
}
