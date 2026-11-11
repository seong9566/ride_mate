namespace RideMateService.Comm.Dto.v1.Auth.Response;

/// <summary>
/// 이 차에 로그인한 계정(로그인 화면의 계정 목록 한 줄)
/// </summary>
public class GetLoginSessionList
{
    /// <summary>
    /// 로그인 세션 ID. 로그인 응답의 session_id와 같다. 앱이 보관한 refresh 토큰과 짝짓는 데 쓴다
    /// </summary>
    public int loginSessionSeq { get; set; }

    /// <summary>
    /// 운전자 이름(엄마·아빠 등)
    /// </summary>
    public string userName { get; set; } = string.Empty;
}
