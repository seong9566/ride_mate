using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace RideMateService.Comm.Security;

/// <summary>
/// AES-256-GCM 전화번호 암호화 서비스
/// </summary>
public class PhoneCryptoService : IPhoneCryptoService
{
    private const byte Version = 0x01;
    private const int NonceSize = 12;
    private const int TagSize = 16;

    // 버전 바이트를 인증 데이터로 묶어 첫 바이트를 바꾸면 복호화가 실패하게 한다
    private static readonly byte[] AssociatedData = { Version };

    private readonly byte[] _phoneEncKey;

    public PhoneCryptoService(IOptions<CryptoOptions> options)
    {
        this._phoneEncKey = Convert.FromBase64String(options.Value.PhoneEncKey);
    }

    /// <summary>
    /// 숫자만 남긴 전화번호를 암호화한다. 결과는 0x01 | nonce(12) | 암호문 | tag(16)이고, 버전 바이트는 인증 데이터로 묶는다.
    /// </summary>
    public byte[] Encrypt(string phoneDigits)
    {
        var plain = Encoding.UTF8.GetBytes(phoneDigits);
        var result = new byte[1 + NonceSize + plain.Length + TagSize];
        result[0] = Version;

        var nonce = result.AsSpan(1, NonceSize);
        var cipher = result.AsSpan(1 + NonceSize, plain.Length);
        var tag = result.AsSpan(1 + NonceSize + plain.Length, TagSize);

        RandomNumberGenerator.Fill(nonce);
        using var aes = new AesGcm(this._phoneEncKey, TagSize);
        aes.Encrypt(nonce, plain, cipher, tag, AssociatedData);
        return result;
    }

    /// <summary>
    /// Encrypt 결과를 복호화한다. 첫 바이트가 0x01이 아니거나 길이가 맞지 않으면 예외를 던진다.
    /// </summary>
    public string Decrypt(byte[] phoneEnc)
    {
        if (phoneEnc.Length < 1 + NonceSize + TagSize || phoneEnc[0] != Version)
            throw new CryptographicException("지원하지 않는 전화번호 암호문 형식입니다.");

        var cipherLength = phoneEnc.Length - 1 - NonceSize - TagSize;
        var nonce = phoneEnc.AsSpan(1, NonceSize);
        var cipher = phoneEnc.AsSpan(1 + NonceSize, cipherLength);
        var tag = phoneEnc.AsSpan(1 + NonceSize + cipherLength, TagSize);

        var plain = new byte[cipherLength];
        using var aes = new AesGcm(this._phoneEncKey, TagSize);
        aes.Decrypt(nonce, cipher, tag, plain, AssociatedData);
        return Encoding.UTF8.GetString(plain);
    }
}
