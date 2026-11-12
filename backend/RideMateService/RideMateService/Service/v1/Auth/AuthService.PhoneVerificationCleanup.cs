namespace RideMateService.Service.v1.Auth;

// 인증번호 행 정리 — 주기 작업(PhoneVerificationCleanupWorker)이 부른다
public partial class AuthService
{
    private const int PhoneVerificationCleanupBatchSize = 500;
    private const int PhoneVerificationCleanupExpiredHours = 24; // 인증하지 않고 만료된 행을 남겨 두는 시간
    private const string AuditSystem = "SYSTEM";

    /// <summary>
    /// 인증번호 행 정리 한 회차
    /// </summary>
    public async Task<int> CleanupPhoneVerificationsService(CancellationToken cancellationToken = default)
    {
        var thisTime = DateTime.Now;
        // 인증 성공 뒤 동의 저장(AddConsent)이 인증 행을 찾는 시간(GuardianConsentValidMinutes) 동안은 남긴다
        var verifiedBefore = thisTime.AddMinutes(-GuardianConsentValidMinutes);
        var expiredBefore = thisTime.AddHours(-PhoneVerificationCleanupExpiredHours);

        var cleaned = 0;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var ids = await _authRepository.GetCleanupPhoneVerificationIdsAsync(verifiedBefore, expiredBefore,
                    PhoneVerificationCleanupBatchSize, cancellationToken).ConfigureAwait(false);
                if (ids.Count == 0)
                    break;

                var updated = await _authRepository.CleanupPhoneVerificationsAsync(ids, verifiedBefore, expiredBefore,
                    thisTime, AuditSystem, cancellationToken).ConfigureAwait(false);
                cleaned += updated;

                // 묶음이 덜 찼으면 대상이 끝났다. 하나도 못 바꿨으면(다른 요청이 먼저 바꿈) 같은 묶음을 되풀이하지 않는다
                if (ids.Count < PhoneVerificationCleanupBatchSize || updated == 0)
                    break;
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested) // 묶음 하나의 오류는 그 회차를 끝낸다(다음 회차에 다시 시도)
            {
                // 예외 본문은 Repository가 이미 남겼다
                _logService.LogMessage($"phone verification cleanup failed: {ex.GetType().Name}");
                break;
            }
        }

        return cleaned;
    }
}
