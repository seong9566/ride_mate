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
    /// 휴대폰 웹 QR 로그인 진입
    /// </summary>
    Task<ResponseModel<WebQrLoginDto>> DetailQrLoginService(string userCode, int? webSessionUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 휴대폰 웹 인증번호 확인·가입
    /// </summary>
    Task<ResponseModel<WebQrLoginDto>> AddWebSessionService(AddWebSessionDto dto, int? currentWebSessionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 휴대폰 웹 QR 로그인 허용
    /// </summary>
    Task<ResponseModel<WebQrLoginDto>> AddQrApprovalService(AddQrApprovalDto dto,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 휴대폰 웹 QR 로그인 취소
    /// </summary>
    Task<ResponseModel<WebQrLoginDto>> DeleteWebQrLoginService(DeleteWebQrLoginDto dto,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 휴대폰 웹 세션 종료
    /// </summary>
    Task<ResponseModel<WebQrLoginDto>> DeleteWebSessionService(DeleteWebSessionDto? dto,
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

    /// <summary>
    /// 이 차에 로그인한 계정 목록
    /// </summary>
    Task<ResponseModel<List<GetLoginSessionList>>> GetLoginSessionListService(string deviceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 보호자 인증번호 발송
    /// </summary>
    Task<ResponseModel<GuardianVerificationResultDto>> AddGuardianVerificationService(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 보호자 인증번호 확인
    /// </summary>
    Task<ResponseModel<GuardianVerificationCheckDto>> UpdateGuardianVerificationService(
        UpdateGuardianVerificationDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// 보호자 동의 저장
    /// </summary>
    Task<ResponseModel<bool>> AddConsentService(AddConsentDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// 인증번호 행 정리 한 회차. 인증 성공 뒤 10분이 지난 행과 인증 없이 만료된 지 24시간이 지난 행의 번호 해시·인증번호 해시를 비우고 삭제 표시한다.
    /// 500행 묶음을 대상이 없을 때까지 반복해 정리한 행 수를 돌려준다. 묶음 하나의 오류는 로그만 남기고 그 회차를 끝낸다. HTTP 요청 밖(주기 작업)에서 부른다
    /// </summary>
    Task<int> CleanupPhoneVerificationsService(CancellationToken cancellationToken = default);
}
