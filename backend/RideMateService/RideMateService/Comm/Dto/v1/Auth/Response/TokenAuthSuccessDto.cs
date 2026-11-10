namespace RideMateService.Comm.Dto.v1.Auth.Response;

/// <summary>
/// 토큰 발급 성공 응답(OAuth 2.0 형식)
/// </summary>
public class TokenAuthSuccessDto
{
    /// <summary>
    /// access 토큰(JWT)
    /// </summary>
    public string access_token { get; set; } = string.Empty;

    /// <summary>
    /// 토큰 종류(Bearer)
    /// </summary>
    public string token_type { get; set; } = "Bearer";

    /// <summary>
    /// access 토큰 유효 시간(초)
    /// </summary>
    public int expires_in { get; set; }

    /// <summary>
    /// 새 refresh 토큰
    /// </summary>
    public string refresh_token { get; set; } = string.Empty;

    /// <summary>
    /// 로그인 세션 ID
    /// </summary>
    public int session_id { get; set; }

    /// <summary>
    /// 초기 설정이 필요한지(실명이 비었거나 아이가 없으면 true)
    /// </summary>
    public bool setup_required { get; set; }
}
