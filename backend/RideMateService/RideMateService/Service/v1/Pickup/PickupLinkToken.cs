using System.Globalization;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;
using RideMateService.Comm.Security;

namespace RideMateService.Service.v1.Pickup;

/// <summary>
/// 픽업 링크 토큰. 원문은 저장하지 않고 PICKUP_SESSION의 TOKEN_NONCE로 다시 계산한다
/// </summary>
public static class PickupLinkToken
{
    /// <summary>
    /// 토큰 생성 난수. 16바이트 CSPRNG 난수의 소문자 hex 32자
    /// </summary>
    public static string NewNonce()
    {
        return Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));
    }

    /// <summary>
    /// 토큰 원문. HMAC("pickup-link:" + 계획 ID + ":" + nonce)의 32바이트를 base64url(패딩 없음) 43자로 쓴다
    /// </summary>
    public static string Create(IHashService hash, int pickupPlanId, string nonce)
    {
        var hex = hash.Hmac("pickup-link:" + pickupPlanId.ToString(CultureInfo.InvariantCulture) + ":" + nonce);
        return Base64UrlEncoder.Encode(Convert.FromHexString(hex));
    }

    /// <summary>
    /// PICKUP_SESSION.TOKEN_HASH 값. HMAC("pickup-link-token:" + 토큰 원문)의 소문자 hex 64자
    /// </summary>
    public static string HashToken(IHashService hash, string token)
    {
        return hash.Hmac("pickup-link-token:" + token);
    }

    /// <summary>
    /// 링크 주소. 설정의 링크 주소 앞부분 + "/" + 토큰
    /// </summary>
    public static string BuildUrl(string baseUrl, string token)
    {
        return baseUrl.TrimEnd('/') + "/" + token;
    }
}
