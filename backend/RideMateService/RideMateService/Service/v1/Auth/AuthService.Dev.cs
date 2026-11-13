using Microsoft.EntityFrameworkCore.Storage;
using RideMateService.Comm;
using RideMateService.Comm.Dto.v1.Auth.Request;
using RideMateService.Comm.Dto.v1.Auth.Response;

namespace RideMateService.Service.v1.Auth;

// 개발 전용 로그인 — 인증번호·QR 없이 계정 ID로 차량 토큰을 낸다. 출시 전까지 배포 서버에서도 열려 있다
public partial class AuthService
{
    private const string DevTokenDefaultDeviceId = "swagger-dev";

    /// <summary>
    /// 개발 전용 토큰 발급
    /// </summary>
    public async Task<ResponseModel<TokenAuthResultDto>> AddDevTokenService(AddDevTokenDto dto,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (dto?.userId is null)
                return TokenError("invalid_request", "userId is required");

            var userId = dto.userId.Value;
            var deviceId = string.IsNullOrWhiteSpace(dto.device_id) ? DevTokenDefaultDeviceId : dto.device_id.Trim();
            return await RetryOnDeadlockAsync(() => AddDevTokenCoreAsync(userId, deviceId, cancellationToken))
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested) // 요청 취소는 그대로 올려보낸다
        {
            LogServiceException(ex);
            return new ResponseModel<TokenAuthResultDto>
                { message = "서버에서 요청을 처리하지 못하였습니다.", data = null, code = 500 };
        }
    }

    private async Task<ResponseModel<TokenAuthResultDto>> AddDevTokenCoreAsync(int userId, string deviceId,
        CancellationToken cancellationToken)
    {
        DateTime thisTime = NowToSecond();
        var transaction = await _context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using var transactionScope = transaction.ConfigureAwait(false);

        // 로그인과 같은 경로로 세션을 만든다 — 계정 잠금, 같은 기기 세션 종료, 새 세션 저장
        var sessionResult = await CreateCarSessionAsync(userId, deviceId, thisTime,
            transaction.GetDbTransaction(), "invalid_grant", "account does not exist", cancellationToken).ConfigureAwait(false);
        if (sessionResult.code != 200)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return sessionResult;
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return sessionResult;
    }
}
