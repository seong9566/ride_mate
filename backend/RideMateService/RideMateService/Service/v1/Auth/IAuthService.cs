using RideMateService.Comm;
using RideMateService.Comm.Dto.v1.Auth.Request;
using RideMateService.Comm.Dto.v1.Auth.Response;

namespace RideMateService.Service.v1.Auth;

public interface IAuthService
{
    /// <summary>
    /// 로그인 인증번호 발송
    /// </summary>
    Task<ResponseModel<PhoneVerificationResultDto>> AddPhoneVerificationService(AddPhoneVerificationDto dto,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 인증번호 로그인
    /// </summary>
    Task<ResponseModel<LoginSessionResultDto>> AddLoginSessionService(AddLoginSessionDto dto,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 토큰 갱신
    /// </summary>
    Task<ResponseModel<LoginSessionResultDto>> UpdateLoginSessionService(UpdateLoginSessionDto dto,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 차량 로그아웃
    /// </summary>
    Task<ResponseModel<bool>> DeleteLoginSessionService(CancellationToken cancellationToken = default);
}
