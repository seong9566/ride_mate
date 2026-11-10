namespace RideMateService.Comm.Security;

/// <summary>
/// 서버 비밀키로 값을 해시한다. 토큰·인증번호·전화번호는 원문 대신 이 해시를 저장한다.
/// </summary>
public interface IHashService
{
    /// <summary>
    /// HMAC-SHA256 해시를 소문자 hex 64자로 돌려준다.
    /// </summary>
    string Hmac(string value);
}
