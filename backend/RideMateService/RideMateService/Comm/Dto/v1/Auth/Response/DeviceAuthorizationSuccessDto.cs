namespace RideMateService.Comm.Dto.v1.Auth.Response;

/// <summary>
/// QR 로그인 발급 성공 응답(OAuth 2.0 기기 인증 형식)
/// </summary>
public class DeviceAuthorizationSuccessDto
{
    /// <summary>
    /// 차량이 승인 결과를 확인할 때 쓰는 코드
    /// </summary>
    public string device_code { get; set; } = string.Empty;

    /// <summary>
    /// QR URL에 넣는 코드
    /// </summary>
    public string user_code { get; set; } = string.Empty;

    /// <summary>
    /// 휴대폰이 접속할 기본 주소
    /// </summary>
    public string verification_uri { get; set; } = string.Empty;

    /// <summary>
    /// user_code를 포함한 완성 주소
    /// </summary>
    public string verification_uri_complete { get; set; } = string.Empty;

    /// <summary>
    /// QR 로그인 유효 시간(초)
    /// </summary>
    public int expires_in { get; set; }

    /// <summary>
    /// 승인 상태 조회 간격(초)
    /// </summary>
    public int interval { get; set; }

    /// <summary>
    /// 차와 휴대폰에 함께 보이는 숫자 4자리
    /// </summary>
    public string match_code { get; set; } = string.Empty;
}
