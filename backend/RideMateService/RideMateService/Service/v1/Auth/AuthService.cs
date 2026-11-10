using System.Security.Claims;
using Microsoft.EntityFrameworkCore.Storage;
using RideMateService.Comm;
using RideMateService.Comm.Dto.v1.Auth.Request;
using RideMateService.Comm.Dto.v1.Auth.Response;
using RideMateService.Comm.Logger;
using RideMateService.Comm.Security;
using RideMateService.Commons.Repository;
using RideMateService.Repository.v1.Auth;

namespace RideMateService.Service.v1.Auth;

public class AuthService : IAuthService
{
    private const string GrantTypeRefreshToken = "refresh_token";
    private const string AuditUser = "PARENT";
    private const int RefreshTokenMaxLength = 128; // 실제 값은 43자다. 더 길면 해시·트랜잭션 전에 거절한다

    private readonly IAuthRepository _authRepository;
    private readonly ILogService _logService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly RideMateContext _context; // 트랜잭션 시작·완료에만 쓴다
    private readonly IHashService _hashService;
    private readonly ITokenService _tokenService;

    public AuthService(IAuthRepository authRepository,
        ILogService logService,
        IHttpContextAccessor httpContextAccessor,
        RideMateContext context,
        IHashService hashService,
        ITokenService tokenService)
    {
        this._authRepository = authRepository;
        this._logService = logService;
        this._httpContextAccessor = httpContextAccessor;
        this._context = context;
        this._hashService = hashService;
        this._tokenService = tokenService;
    }

    /// <summary>
    /// 토큰 발급
    /// </summary>
    public async Task<ResponseModel<TokenAuthResultDto>> TokenAuthService(TokenAuthDto dto, CancellationToken cancellationToken = default)
    {
        try
        {
            // 1) 입력 검증 — 방식별 필수 값은 Service에서 본다(DTO에 검증 특성을 붙이지 않는다)
            if (dto is null || string.IsNullOrWhiteSpace(dto.grant_type))
                return TokenError("invalid_request", "grant_type is required");

            if (dto.grant_type != GrantTypeRefreshToken)
                return TokenError("unsupported_grant_type", "grant_type is not supported");

            if (string.IsNullOrWhiteSpace(dto.refresh_token))
                return TokenError("invalid_request", "refresh_token is required");

            if (dto.refresh_token.Length > RefreshTokenMaxLength)
                return TokenError("invalid_grant", "refresh token is invalid or expired");

            DateTime thisTime = DateTime.Now;
            var requestHash = _hashService.Hmac(dto.refresh_token);

            // 2) 세션 잠금 — 트랜잭션의 첫 문장으로 행을 FOR UPDATE로 잠근다.
            //    예외로 빠져나가면 await using이 트랜잭션을 롤백한다
            var transaction = await _context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await using var transactionScope = transaction.ConfigureAwait(false);

            var loginSessionId = await _authRepository.LockLoginSessionByRefreshAsync(requestHash,
                thisTime.AddHours(-ITokenService.RefreshGraceHours), thisTime,
                transaction.GetDbTransaction(), cancellationToken).ConfigureAwait(false);
            if (loginSessionId is null)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return TokenError("invalid_grant", "refresh token is invalid or expired");
            }

            var session = await _authRepository.GetLoginSessionAsync(loginSessionId.Value, cancellationToken).ConfigureAwait(false);
            if (session is null)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return TokenError("invalid_grant", "refresh token is invalid or expired");
            }

            // 3) refresh 교체 — 새 값은 세션과 낸 값으로 계산하므로 같은 값을 내면 늘 같은 새 값이 나온다.
            //    지금 값이면 직전 값으로 넘기고 계산한 값을 지금 값으로 둔다.
            //    직전 값이면(재시도·늦게 처리된 중복) 계산한 값이 이미 지금 값이므로 행의 값은 그대로 두고 같은 값을 다시 준다
            var newRefreshToken = _tokenService.DeriveNextRefreshToken(session.LoginSessionId, dto.refresh_token);
            var newTokenHash = _hashService.Hmac(newRefreshToken);

            if (session.TokenHash == requestHash)
            {
                session.PrevTokenHash = session.TokenHash;
                session.TokenHash = newTokenHash;
                session.RotateDt = thisTime;
            }
            else if (session.PrevTokenHash == requestHash)
            {
                // 계산한 값이 지금 값과 다르면(이전 코드가 난수로 교체한 행, 해시 키 변경) 받아 줄 값이 없다
                if (newTokenHash != session.TokenHash)
                {
                    await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    return TokenError("invalid_grant", "refresh token is invalid or expired");
                }
            }
            else
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return TokenError("invalid_grant", "refresh token is invalid or expired");
            }

            session.ExpireDt = thisTime.AddDays(ITokenService.RefreshTokenDays);
            session.LastUsedDt = thisTime;
            session.UpdateDt = thisTime;
            session.UpdateUser = AuditUser;

            // 4) 저장
            if (!await _authRepository.UpdateLoginSessionAsync(session, cancellationToken).ConfigureAwait(false))
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return new ResponseModel<TokenAuthResultDto> { message = "서버에서 요청을 처리하지 못하였습니다.", data = null, code = 500 };
            }

            // 5) 계정 확인 — 계정이 삭제됐으면 교체를 되돌린다
            var setupRequired = await _authRepository.GetSetupRequiredAsync(session.UserId,
                transaction.GetDbTransaction(), cancellationToken).ConfigureAwait(false);
            if (setupRequired is null)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return TokenError("invalid_grant", "refresh token is invalid or expired");
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            // 6) 응답 — 커밋한 뒤에만 토큰을 내준다
            var result = new TokenAuthResultDto
            {
                success = new TokenAuthSuccessDto
                {
                    access_token = _tokenService.CreateAccessToken(session.UserId, session.LoginSessionId, session.DeviceId, thisTime),
                    token_type = "Bearer",
                    expires_in = ITokenService.AccessTokenSeconds,
                    refresh_token = newRefreshToken,
                    session_id = session.LoginSessionId,
                    setup_required = setupRequired.Value
                }
            };

            return new ResponseModel<TokenAuthResultDto> { message = "요청이 정상 처리되었습니다.", data = result, code = 200 };
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested) // 요청 취소는 그대로 올려보낸다
        {
            _logService.LogMessage(ex.ToString());
#if DEBUG
            _logService.ConsoleWarning(ex.ToString());
#endif
            return new ResponseModel<TokenAuthResultDto> { message = "서버에서 요청을 처리하지 못하였습니다.", data = null, code = 500 };
        }
    }

    /// <summary>
    /// 차량 로그아웃
    /// </summary>
    public async Task<ResponseModel<bool>> DeleteLoginSessionService(CancellationToken cancellationToken = default)
    {
        try
        {
            // 1) 인증 확인 — 로그인 세션은 access 토큰의 sid 클레임에서 꺼낸다
            var sid = _httpContextAccessor.HttpContext?.User.FindFirstValue("sid");
            if (!int.TryParse(sid, out var loginSessionId))
                return new ResponseModel<bool> { message = "잘못된 요청입니다.", data = false, code = 401 };

            // 2) 세션 종료 — 이미 끝난 세션이면 할 일이 없다
            var session = await _authRepository.GetLoginSessionAsync(loginSessionId, cancellationToken).ConfigureAwait(false);
            if (session is not null)
            {
                DateTime thisTime = DateTime.Now;
                session.DeleteYn = true;
                session.UpdateDt = thisTime;
                session.UpdateUser = AuditUser;

                if (!await _authRepository.UpdateLoginSessionAsync(session, cancellationToken).ConfigureAwait(false))
                    return new ResponseModel<bool> { message = "서버에서 요청을 처리하지 못하였습니다.", data = false, code = 500 };
            }

            return new ResponseModel<bool> { message = "요청이 정상 처리되었습니다.", data = true, code = 200 };
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested) // 요청 취소는 그대로 올려보낸다
        {
            _logService.LogMessage(ex.ToString());
#if DEBUG
            _logService.ConsoleWarning(ex.ToString());
#endif
            return new ResponseModel<bool> { message = "서버에서 요청을 처리하지 못하였습니다.", data = false, code = 500 };
        }
    }

    // OAuth 실패 응답(400). 설명에는 값이나 내부 사정을 넣지 않는다
    private static ResponseModel<TokenAuthResultDto> TokenError(string error, string errorDescription)
    {
        return new ResponseModel<TokenAuthResultDto>
        {
            message = "잘못된 요청입니다.",
            data = new TokenAuthResultDto { error = new TokenAuthErrorDto { error = error, error_description = errorDescription } },
            code = 400
        };
    }
}
