namespace RideMateService.Comm.Dto.v1.Auth.Request;

/// <summary>
/// 토큰 발급 요청(form). OAuth 2.0 형식이라 form 필드 이름을 그대로 쓰고, 검증은 Service에서 한다
/// </summary>
public class TokenAuthDto
{
    /// <summary>
    /// 발급 방식(refresh_token)
    /// </summary>
    public string? grant_type { get; set; }

    /// <summary>
    /// refresh 토큰 원문
    /// </summary>
    public string? refresh_token { get; set; }
}
