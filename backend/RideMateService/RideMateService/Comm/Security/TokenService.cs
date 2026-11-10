using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace RideMateService.Comm.Security;

/// <summary>
/// access 토큰(JWT HS256)과 refresh 토큰(첫 발급은 난수, 교체는 해시 키로 계산)을 만든다.
/// </summary>
public class TokenService : ITokenService
{
    private readonly JwtOptions _jwtOptions;
    private readonly SigningCredentials _signingCredentials;
    private readonly JsonWebTokenHandler _handler = new JsonWebTokenHandler();
    private readonly byte[] _hashKey;

    public TokenService(IOptions<JwtOptions> options, IOptions<CryptoOptions> cryptoOptions)
    {
        this._jwtOptions = options.Value;
        var key = new SymmetricSecurityKey(Convert.FromBase64String(this._jwtOptions.SigningKey));
        this._signingCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        this._hashKey = Convert.FromBase64String(cryptoOptions.Value.HashKey);
    }

    /// <summary>
    /// HS256 access 토큰을 만든다. 클레임은 sub(사용자), sid(로그인 세션), did(기기)이고 만료는 thisTime + 3600초다.
    /// </summary>
    public string CreateAccessToken(int userId, int loginSessionId, string deviceId, DateTime thisTime)
    {
        // thisTime은 서버 로컬 시각이다. 토큰의 시각은 UTC 시점으로 바꿔 넣는다.
        var issuedAt = thisTime.ToUniversalTime();

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = this._jwtOptions.Issuer,
            Audience = this._jwtOptions.Audience,
            IssuedAt = issuedAt,
            NotBefore = issuedAt,
            Expires = issuedAt.AddSeconds(ITokenService.AccessTokenSeconds),
            SigningCredentials = this._signingCredentials,
            Claims = new Dictionary<string, object>
            {
                ["sub"] = userId.ToString(),
                ["sid"] = loginSessionId.ToString(),
                ["did"] = deviceId
            }
        };
        return this._handler.CreateToken(descriptor);
    }

    /// <summary>
    /// 32바이트 난수를 base64url로 만든다.
    /// </summary>
    public string CreateRefreshToken()
    {
        return Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
    }

    /// <summary>
    /// 교체할 refresh 값을 만든다. base64url(HMAC-SHA256(Crypto:HashKey, "refresh-next:" + 세션ID + ":" + 지금 낸 값))이다.
    /// 같은 입력이면 늘 같은 값이 나오므로, 직전 값으로 다시 보낸 요청도 처음 요청과 같은 새 값을 받는다.
    /// 원문은 어디에도 저장하지 않는다.
    /// </summary>
    public string DeriveNextRefreshToken(int loginSessionId, string currentRefreshToken)
    {
        var message = "refresh-next:" + loginSessionId.ToString(CultureInfo.InvariantCulture) + ":" + currentRefreshToken;
        return Base64UrlEncoder.Encode(HMACSHA256.HashData(this._hashKey, Encoding.UTF8.GetBytes(message)));
    }
}
