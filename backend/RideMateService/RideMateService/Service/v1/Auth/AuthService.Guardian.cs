using System.Security.Claims;
using Microsoft.EntityFrameworkCore.Storage;
using RideMateService.Comm;
using RideMateService.Comm.Dto.v1.Auth.Request;
using RideMateService.Comm.Dto.v1.Auth.Response;
using RideMateService.Commons.DBModels;

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
            var user = await GetLoginUserAsync(cancellationToken).ConfigureAwait(false);
            if (user is null)
                return new ResponseModel<GuardianVerificationResultDto>
                    { message = "잘못된 요청입니다.", data = null, code = 401 };

            var phoneDigits = _phoneCryptoService.Decrypt(user.PhoneEnc);
            var phoneMasked = MaskPhoneTail(phoneDigits); // 커밋·문자 발송 뒤에 실패하지 않도록 미리 만든다

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
                        phoneMasked = phoneMasked
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

    /// <summary>
    /// 보호자 인증번호 확인
    /// </summary>
    public Task<ResponseModel<GuardianVerificationCheckDto>> UpdateGuardianVerificationService(
        UpdateGuardianVerificationDto dto, CancellationToken cancellationToken = default)
    {
        return RetryOnDeadlockAsync(() => UpdateGuardianVerificationCoreAsync(dto, cancellationToken));
    }

    // 확인은 PHONE_VERIFICATION 행만 잠그고 수정한다. 교착은 RetryOnDeadlockAsync가 다시 실행하므로 500으로 바꾸지 않고 올려보낸다
    private async Task<ResponseModel<GuardianVerificationCheckDto>> UpdateGuardianVerificationCoreAsync(
        UpdateGuardianVerificationDto dto, CancellationToken cancellationToken)
    {
        try
        {
            // 1) 입력 검증 — API 모델 검증을 거치지 않은 호출도 같은 규칙으로 거절한다
            if (dto is null || !IsValidCode(dto.code))
                return GuardianCheckResponse(400, "잘못된 요청입니다.", data: null);

            // 2) 인증 확인
            var user = await GetLoginUserAsync(cancellationToken).ConfigureAwait(false);
            if (user is null)
                return GuardianCheckResponse(401, "잘못된 요청입니다.", data: null);

            DateTime thisTime = NowToSecond();
            var phoneDigits = _phoneCryptoService.Decrypt(user.PhoneEnc);
            var transaction = await _context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await using var transactionScope = transaction.ConfigureAwait(false);

            // 3) 일반 조회보다 먼저 번호 전체 → GUARDIAN 순으로 잠근다
            var latestId = await _authRepository.LockLatestPhoneVerificationAsync(user.PhoneHash,
                transaction.GetDbTransaction(), cancellationToken).ConfigureAwait(false);
            var verificationId = await _authRepository.LockLatestPhoneVerificationByPurposeAsync(user.PhoneHash,
                "GUARDIAN", transaction.GetDbTransaction(), cancellationToken).ConfigureAwait(false);

            // 4) 잠금·만료·불일치 판정은 휴대폰 웹 인증번호 확인과 같은 공통 로직을 쓴다
            var check = await CheckPhoneVerificationAsync(latestId, verificationId, phoneDigits, dto.code!, thisTime,
                transaction, cancellationToken).ConfigureAwait(false);
            if (check.Outcome != PhoneVerificationCheckOutcome.Matched)
            {
                return check.Outcome switch
                {
                    PhoneVerificationCheckOutcome.Locked => GuardianCheckResponse(102, "잠시 후 다시 시도해 주세요.",
                        new GuardianVerificationCheckDto { retryAfter = check.RetryAfter }),
                    PhoneVerificationCheckOutcome.NotRequested or PhoneVerificationCheckOutcome.Expired =>
                        GuardianCheckResponse(103, "인증 시간이 지났습니다. 인증번호를 다시 받아 주세요.",
                            new GuardianVerificationCheckDto()),
                    PhoneVerificationCheckOutcome.Mismatch => GuardianCheckResponse(101, "인증번호가 맞지 않습니다.",
                        new GuardianVerificationCheckDto { remainingAttempts = check.RemainingAttempts }),
                    _ => GuardianCheckResponse(500, "서버에서 요청을 처리하지 못하였습니다.", data: null)
                };
            }

            // 5) 인증 성공 기록
            var verification = check.Verification!;
            verification.VerifyDt = thisTime;
            verification.UpdateDt = thisTime;
            verification.UpdateUser = AuditUser;
            if (!await _authRepository.UpdatePhoneVerificationAsync(verification, cancellationToken).ConfigureAwait(false))
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return GuardianCheckResponse(500, "서버에서 요청을 처리하지 못하였습니다.", data: null);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return GuardianCheckResponse(200, "요청이 정상 처리되었습니다.", new GuardianVerificationCheckDto());
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested && !IsDeadlock(ex)) // 요청 취소·교착은 그대로 올려보낸다
        {
            LogServiceException(ex);
            return GuardianCheckResponse(500, "서버에서 요청을 처리하지 못하였습니다.", data: null);
        }
    }

    // 로그인한 계정(JWT sub의 사용자) — 클레임이 없거나 계정이 없으면 null. 부른 쪽이 401로 바꾼다
    private async Task<UserInfo?> GetLoginUserAsync(CancellationToken cancellationToken)
    {
        if (!int.TryParse(_httpContextAccessor.HttpContext?.User.FindFirstValue("sub"), out var userId))
            return null;

        return await _authRepository.GetUserInfoAsync(userId, cancellationToken).ConfigureAwait(false);
    }

    private static ResponseModel<GuardianVerificationCheckDto> GuardianCheckResponse(int code, string message,
        GuardianVerificationCheckDto? data)
    {
        return new ResponseModel<GuardianVerificationCheckDto> { message = message, data = data, code = code };
    }

    // 보호자 화면용 가린 번호 — 뒤 4자리를 가린다(11자리 010-1234-****, 10자리 011-123-****).
    // 가운데를 가리는 휴대폰 웹용 MaskPhone(010-****-1234)과 형식이 달라 이름을 구분한다
    private static string MaskPhoneTail(string phoneDigits)
    {
        return phoneDigits[..3] + "-" + phoneDigits[3..^4] + "-****";
    }
}
