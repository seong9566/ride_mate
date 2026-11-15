namespace RideMateService.Comm.Dto.v1.Auth.Response;

/// <summary>
/// 토큰 발급 결과. 성공이면 success, 실패면 error 하나만 채운다
/// </summary>
public class TokenAuthResultDto
{
    /// <summary>
    /// 성공 응답
    /// </summary>
    public TokenAuthSuccessDto? success { get; set; }

    /// <summary>
    /// 실패 응답
    /// </summary>
    public TokenAuthErrorDto? error { get; set; }
}
