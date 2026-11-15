namespace RideMateService.Comm.Map;

/// <summary>
/// 카카오 API 설정. 설정 절 "Kakao".
/// </summary>
public class KakaoOptions
{
    /// <summary>
    /// 카카오 REST API 키. 비어 있으면 카카오 API를 부르지 않는다. 비밀값이므로 환경변수 Kakao__RestApiKey로 주입한다.
    /// </summary>
    public string RestApiKey { get; set; } = string.Empty;
}
