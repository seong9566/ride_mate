namespace RideMateService.Comm.Security;

/// <summary>
/// 전화번호를 암호화해 저장하고 복호화해 읽는다.
/// </summary>
public interface IPhoneCryptoService
{
    /// <summary>
    /// 숫자만 남긴 전화번호를 암호화한다. 결과는 0x01 | nonce(12) | 암호문 | tag(16)이다.
    /// </summary>
    byte[] Encrypt(string phoneDigits);

    /// <summary>
    /// Encrypt 결과를 복호화한다. 첫 바이트가 0x01이 아니거나 길이가 맞지 않으면 예외를 던진다.
    /// </summary>
    string Decrypt(byte[] phoneEnc);
}
