namespace RideMateService.Comm.Dto.v1.Auth.Request;

/// <summary>
/// QR 로그인 발급 요청(form). OAuth 2.0 형식이라 form 필드 이름을 그대로 쓰고, 검증은 Service에서 한다
/// </summary>
public class DeviceAuthorizationDto
{
    /// <summary>
    /// 차량 앱 설치 번호
    /// </summary>
    public string? device_id { get; set; }
}
