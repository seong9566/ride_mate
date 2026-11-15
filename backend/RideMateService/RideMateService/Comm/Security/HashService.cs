using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace RideMateService.Comm.Security;

/// <summary>
/// HMAC-SHA256 해시 서비스
/// </summary>
public class HashService : IHashService
{
    private readonly byte[] _hashKey;

    public HashService(IOptions<CryptoOptions> options)
    {
        this._hashKey = Convert.FromBase64String(options.Value.HashKey);
    }

    /// <summary>
    /// HMAC-SHA256 해시를 소문자 hex 64자로 돌려준다.
    /// </summary>
    public string Hmac(string value)
    {
        var hash = HMACSHA256.HashData(this._hashKey, Encoding.UTF8.GetBytes(value));
        return Convert.ToHexStringLower(hash);
    }
}
