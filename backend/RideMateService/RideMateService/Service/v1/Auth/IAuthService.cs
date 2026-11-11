using RideMateService.Comm;
using RideMateService.Comm.Dto.v1.Auth.Request;
using RideMateService.Comm.Dto.v1.Auth.Response;

namespace RideMateService.Service.v1.Auth;

public interface IAuthService
{
    /// <summary>
    /// QR 로그인 발급
    /// </summary>
    Task<ResponseModel<DeviceAuthorizationResultDto>> DeviceAuthorizationService(DeviceAuthorizationDto dto,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 로그인 인증번호 발송
    /// </summary>
    Task<ResponseModel<PhoneVerificationResultDto>> AddPhoneVerificationService(AddPhoneVerificationDto dto,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 토큰 발급
    /// </summary>
    Task<ResponseModel<TokenAuthResultDto>> TokenAuthService(TokenAuthDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// 차량 QR 로그인 취소
    /// </summary>
    Task<ResponseModel<bool>> DeleteQrLoginService(DeleteQrLoginDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// 차량 로그아웃
    /// </summary>
    Task<ResponseModel<bool>> DeleteLoginSessionService(CancellationToken cancellationToken = default);
}
