using System.Security.Claims;
using RideMateService.Comm;
using RideMateService.Comm.Dto.v1.Auth.Response;

namespace RideMateService.Service.v1.Auth;

// 초기 설정 1/4 보호자 동의 — 보호자 인증번호 발송·확인과 동의 저장
public partial class AuthService
{
    private const int GuardianConsentValidMinutes = 10; // 인증 성공 뒤 동의 저장을 허용하는 시간

    /// <summary>
    /// 보호자 인증번호 발송
    /// </summary>
    public Task<ResponseModel<GuardianVerificationResultDto>> AddGuardianVerificationService(
        CancellationToken cancellationToken = default)
    {
        return RetryOnDeadlockAsync(() => AddGuardianVerificationCoreAsync(cancellationToken));
    }

    // 처음 보는 번호는 FOR UPDATE가 행 없이 갭 락만 잡아, 같은 번호의 동시 발송이 INSERT에서 교착할 수 있다.
    // 교착은 RetryOnDeadlockAsync가 한 번 다시 실행하므로 여기서는 500으로 바꾸지 않고 올려보낸다
    private async Task<ResponseModel<GuardianVerificationResultDto>> AddGuardianVerificationCoreAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            // 1) 인증 확인 — 번호는 요청으로 받지 않고 로그인한 계정에서 꺼낸다
            if (!int.TryParse(_httpContextAccessor.HttpContext?.User.FindFirstValue("sub"), out var userId))
                return new ResponseModel<GuardianVerificationResultDto>
                    { message = "잘못된 요청입니다.", data = null, code = 401 };

            var user = await _authRepository.GetUserInfoAsync(userId, cancellationToken).ConfigureAwait(false);
            if (user is null)
                return new ResponseModel<GuardianVerificationResultDto>
                    { message = "잘못된 요청입니다.", data = null, code = 401 };

            var phoneDigits = _phoneCryptoService.Decrypt(user.PhoneEnc);

            // 2) 발송 — 잠금·재발송 간격·저장·문자 발송은 로그인 인증번호 발송과 같은 공통 로직을 쓴다
            var result = await SendPhoneVerificationCoreAsync(phoneDigits, user.PhoneHash, "GUARDIAN",
                code => "[라이드메이트] 보호자 확인 인증번호 " + code + " (3분 안에 입력해 주세요)", cancellationToken).ConfigureAwait(false);

            // 3) 응답 — 모든 분기(200·101·102)에 가린 번호를 채운다
            return new ResponseModel<GuardianVerificationResultDto>
            {
                message = result.message,
                data = result.data is null
                    ? null
                    : new GuardianVerificationResultDto
                    {
                        expiresIn = result.data.expiresIn,
                        retryAfter = result.data.retryAfter,
                        phoneMasked = MaskPhoneTail(phoneDigits)
                    },
                code = result.code
            };
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested && !IsDeadlock(ex)) // 요청 취소·교착은 그대로 올려보낸다
        {
            LogServiceException(ex);
            return new ResponseModel<GuardianVerificationResultDto>
                { message = "서버에서 요청을 처리하지 못하였습니다.", data = null, code = 500 };
        }
    }

    // 보호자 화면용 가린 번호 — 뒤 4자리를 가린다(11자리 010-1234-****, 10자리 011-123-****).
    // 가운데를 가리는 휴대폰 웹용 MaskPhone(010-****-1234)과 형식이 달라 이름을 구분한다
    private static string MaskPhoneTail(string phoneDigits)
    {
        return phoneDigits[..3] + "-" + phoneDigits[3..^4] + "-****";
    }
}
