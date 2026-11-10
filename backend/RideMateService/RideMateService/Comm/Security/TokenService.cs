using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace RideMateService.Comm.Security;

/// <summary>
/// access 토큰(JWT HS256)과 refresh 토큰(난수)을 만든다.
/// </summary>
public class TokenService : ITokenService
{
    private readonly JwtOptions _jwtOptions;
    private readonly SigningCredentials _signingCredentials;
    private readonly JsonWebTokenHandler _handler = new JsonWebTokenHandler();

    public TokenService(IOptions<JwtOptions> options)
    {
        this._jwtOptions = options.Value;
        var key = new SymmetricSecurityKey(Convert.FromBase64String(this._jwtOptions.SigningKey));
        this._signingCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
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
}
