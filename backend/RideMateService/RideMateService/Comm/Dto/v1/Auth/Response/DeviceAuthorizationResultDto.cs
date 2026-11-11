namespace RideMateService.Comm.Dto.v1.Auth.Response;

/// <summary>
/// QR 로그인 발급 결과. 성공이면 success, 실패면 error 하나만 채운다
/// </summary>
public class DeviceAuthorizationResultDto
{
    /// <summary>
    /// 성공 응답
    /// </summary>
    public DeviceAuthorizationSuccessDto? success { get; set; }

    /// <summary>
    /// 실패 응답
    /// </summary>
    public TokenAuthErrorDto? error { get; set; }
}
