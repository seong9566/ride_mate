using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore.Storage;
using MySql.Data.MySqlClient;
using RideMateService.Comm;
using RideMateService.Comm.Dto.v1.Auth.Request;
using RideMateService.Comm.Dto.v1.Auth.Response;
using RideMateService.Comm.Logger;
using RideMateService.Comm.Security;
using RideMateService.Comm.Sms;
using RideMateService.Commons.DBModels;
using RideMateService.Commons.Repository;
using RideMateService.Repository.v1.Auth;

namespace RideMateService.Service.v1.Auth;

public class AuthService : IAuthService
{
    private const int PhoneVerificationExpireMinutes = 3;
    private const int PhoneVerificationResendSeconds = 60;
    private const int PhoneVerificationCarryMinutes = 10;
    private const int PhoneVerificationMaxFailCount = 5;
    private const int PhoneVerificationLockMinutes = 10;
    private const string GrantTypeOtp = "otp";
    private const string GrantTypeRefreshToken = "refresh_token";
    private const string AuditUser = "PARENT";
    private const int DeadlockMaxAttempts = 3;
    private const int RefreshTokenMaxLength = 128; // 실제 값은 43자다. 더 길면 해시·트랜잭션 전에 거절한다

    private readonly IAuthRepository _authRepository;
    private readonly ILogService _logService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly RideMateContext _context; // 트랜잭션 시작·완료에만 쓴다
    private readonly IHashService _hashService;
    private readonly ISmsSender _smsSender;
    private readonly ITokenService _tokenService;

    public AuthService(IAuthRepository authRepository,
        ILogService logService,
        IHttpContextAccessor httpContextAccessor,
        RideMateContext context,
        IHashService hashService,
        ISmsSender smsSender,
        ITokenService tokenService)
    {
        this._authRepository = authRepository;
        this._logService = logService;
        this._httpContextAccessor = httpContextAccessor;
        this._context = context;
        this._hashService = hashService;
        this._smsSender = smsSender;
        this._tokenService = tokenService;
    }

    /// <summary>
    /// 로그인 인증번호 발송
    /// </summary>
    public Task<ResponseModel<PhoneVerificationResultDto>> AddPhoneVerificationService(AddPhoneVerificationDto dto,
        CancellationToken cancellationToken = default)
    {
        return RetryOnDeadlockAsync(() => AddPhoneVerificationCoreAsync(dto, cancellationToken));
    }

    // 처음 보는 번호는 FOR UPDATE가 행 없이 갭 락만 잡아, 같은 번호의 동시 발송이 INSERT에서 교착할 수 있다.
    // 교착은 RetryOnDeadlockAsync가 한 번 다시 실행하므로 여기서는 500으로 바꾸지 않고 올려보낸다
    private async Task<ResponseModel<PhoneVerificationResultDto>> AddPhoneVerificationCoreAsync(AddPhoneVerificationDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            DateTime thisTime = NowToSecond(); // DB DATETIME은 초 단위로 반올림해 저장하므로 기준 시각도 초 단위로 맞춘다

            // 1) 입력 검증 — API 모델 검증을 거치지 않은 호출도 같은 규칙으로 거절한다
            if (dto is null || !IsValidPhone(dto.phone))
                return new ResponseModel<PhoneVerificationResultDto> { message = "잘못된 요청입니다.", data = null, code = 400 };

            var phoneDigits = dto.phone!;
            var phoneHash = CreatePhoneHash(phoneDigits);
            var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
            var codeHash = CreateCodeHash(phoneDigits, code);

            // 2) 최근 발송 잠금 — 용도와 관계없이 같은 번호의 마지막 행을 기준으로 제한한다
            var transaction = await _context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await using var transactionScope = transaction.ConfigureAwait(false);

            var latestId = await _authRepository.LockLatestPhoneVerificationAsync(phoneHash,
                transaction.GetDbTransaction(), cancellationToken).ConfigureAwait(false);
            var latest = latestId is null
                ? null
                : await _authRepository.GetPhoneVerificationAsync(latestId.Value, cancellationToken).ConfigureAwait(false);

            // 3) 잠금 확인
            if (latest?.LockUntilDt > thisTime)
            {
                var retryAfter = RemainingSeconds(latest.LockUntilDt.Value, thisTime);
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return new ResponseModel<PhoneVerificationResultDto>
                {
                    message = "잘못된 요청입니다.",
                    data = new PhoneVerificationResultDto { expiresIn = 0, retryAfter = retryAfter },
                    code = 102
                };
            }

            // 4) 재발송 간격 확인
            if (latest?.CreateDt > thisTime.AddSeconds(-PhoneVerificationResendSeconds))
            {
                var retryAfter = Math.Max(1,
                    RemainingSeconds(latest.CreateDt.AddSeconds(PhoneVerificationResendSeconds), thisTime));
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return new ResponseModel<PhoneVerificationResultDto>
                {
                    message = "잘못된 요청입니다.",
                    data = new PhoneVerificationResultDto { expiresIn = 0, retryAfter = retryAfter },
                    code = 101
                };
            }

            // 5) 최근 미완료 인증의 실패 횟수 이어받기
            var failCount = latest is not null
                            && latest.VerifyDt is null
                            && latest.LockUntilDt is null
                            && latest.CreateDt > thisTime.AddMinutes(-PhoneVerificationCarryMinutes)
                ? latest.FailCnt
                : (sbyte)0;

            // 6) 새 인증번호 저장
            var verification = new PhoneVerification
            {
                Purpose = "LOGIN",
                PhoneHash = phoneHash,
                CodeHash = codeHash,
                ExpireDt = thisTime.AddMinutes(PhoneVerificationExpireMinutes),
                FailCnt = failCount,
                CreateDt = thisTime,
                CreateUser = AuditUser,
                UpdateDt = thisTime,
                UpdateUser = AuditUser,
                DeleteYn = false
            };

            if (!await _authRepository.AddPhoneVerificationAsync(verification, cancellationToken).ConfigureAwait(false))
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return new ResponseModel<PhoneVerificationResultDto>
                    { message = "서버에서 요청을 처리하지 못하였습니다.", data = null, code = 500 };
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            // 7) DB 반영 뒤 문자 발송 — 커밋한 뒤에는 요청이 끊겨도 보낸다(행만 남고 1분 동안 다시 받지 못하는 일을 막는다)
            await _smsSender.SendAsync(phoneDigits,
                "[라이드메이트] 인증번호 " + code + " (3분 안에 입력해 주세요)", CancellationToken.None).ConfigureAwait(false);

            // 8) 응답
            return new ResponseModel<PhoneVerificationResultDto>
            {
                message = "요청이 정상 처리되었습니다.",
                data = new PhoneVerificationResultDto
                    { expiresIn = PhoneVerificationExpireMinutes * 60, retryAfter = PhoneVerificationResendSeconds },
                code = 200
            };
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested && !IsDeadlock(ex)) // 요청 취소·교착은 그대로 올려보낸다
        {
            _logService.LogMessage(ex.ToString());
#if DEBUG
            _logService.ConsoleWarning(ex.ToString());
#endif
            return new ResponseModel<PhoneVerificationResultDto>
                { message = "서버에서 요청을 처리하지 못하였습니다.", data = null, code = 500 };
        }
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

            return dto.grant_type switch
            {
                GrantTypeRefreshToken => await RefreshTokenGrantAsync(dto, cancellationToken).ConfigureAwait(false),
                GrantTypeOtp => await RetryOnDeadlockAsync(() => OtpGrantAsync(dto, cancellationToken)).ConfigureAwait(false),
                _ => TokenError("unsupported_grant_type", "grant_type is not supported")
            };
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

    private async Task<ResponseModel<TokenAuthResultDto>> RefreshTokenGrantAsync(TokenAuthDto dto,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(dto.refresh_token))
            return TokenError("invalid_request", "refresh_token is required");

        if (dto.refresh_token.Length > RefreshTokenMaxLength)
            return TokenError("invalid_grant", "refresh token is invalid or expired");

        DateTime thisTime = DateTime.Now;
        var requestHash = _hashService.Hmac(dto.refresh_token);

        // 1) 세션 잠금 — 트랜잭션의 첫 문장으로 행을 FOR UPDATE로 잠근다.
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

        // 2) refresh 교체 — 새 값은 세션과 낸 값으로 계산하므로 같은 값을 내면 늘 같은 새 값이 나온다.
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

        // 3) 저장
        if (!await _authRepository.UpdateLoginSessionAsync(session, cancellationToken).ConfigureAwait(false))
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return new ResponseModel<TokenAuthResultDto>
                { message = "서버에서 요청을 처리하지 못하였습니다.", data = null, code = 500 };
        }

        // 4) 계정 확인 — 계정이 삭제됐으면 교체를 되돌린다
        var setupRequired = await _authRepository.GetSetupRequiredAsync(session.UserId,
            transaction.GetDbTransaction(), cancellationToken).ConfigureAwait(false);
        if (setupRequired is null)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return TokenError("invalid_grant", "refresh token is invalid or expired");
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        // 5) 응답 — 커밋한 뒤에만 토큰을 내준다
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

    private async Task<ResponseModel<TokenAuthResultDto>> OtpGrantAsync(TokenAuthDto dto,
        CancellationToken cancellationToken)
    {
        DateTime thisTime = NowToSecond(); // DB DATETIME은 초 단위로 반올림해 저장하므로 기준 시각도 초 단위로 맞춘다

        // 1) 입력 검증
        if (!IsValidPhone(dto.phone)
            || !IsValidCode(dto.code)
            || string.IsNullOrWhiteSpace(dto.device_id)
            || dto.device_id.Length > 100)
            return TokenError("invalid_request", "otp request is invalid");

        var phoneDigits = dto.phone!;
        var code = dto.code!;
        var deviceId = dto.device_id!;
        var phoneHash = CreatePhoneHash(phoneDigits);

        var transaction = await _context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using var transactionScope = transaction.ConfigureAwait(false);

        // 2) 번호의 최근 인증 잠금 확인
        var latestId = await _authRepository.LockLatestPhoneVerificationAsync(phoneHash,
            transaction.GetDbTransaction(), cancellationToken).ConfigureAwait(false);
        var latest = latestId is null
            ? null
            : await _authRepository.GetPhoneVerificationAsync(latestId.Value, cancellationToken).ConfigureAwait(false);
        if (latest?.LockUntilDt > thisTime)
        {
            var retryAfter = RemainingSeconds(latest.LockUntilDt.Value, thisTime);
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return TokenError("otp_locked", "verification is temporarily locked", retryAfter: retryAfter);
        }

        // 3) 로그인용 최근 인증번호 잠금
        var verificationId = await _authRepository.LockLatestPhoneVerificationByPurposeAsync(phoneHash, "LOGIN",
            transaction.GetDbTransaction(), cancellationToken).ConfigureAwait(false);
        var verification = verificationId is null
            ? null
            : await _authRepository.GetPhoneVerificationAsync(verificationId.Value, cancellationToken).ConfigureAwait(false);
        if (verification is null || verification.VerifyDt is not null)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return TokenError("invalid_grant", "no pending verification code");
        }

        // 다른 용도의 행이 더 최근이어도 이 행이 잠겼으면 거절한다
        if (verification.LockUntilDt > thisTime)
        {
            var retryAfter = RemainingSeconds(verification.LockUntilDt.Value, thisTime);
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return TokenError("otp_locked", "verification is temporarily locked", retryAfter: retryAfter);
        }

        // 4) 만료 확인
        if (verification.ExpireDt <= thisTime)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return TokenError("otp_expired", "verification code has expired");
        }

        // 5) 인증번호 불일치 기록
        if (verification.CodeHash != CreateCodeHash(phoneDigits, code))
        {
            verification.FailCnt = (sbyte)(verification.FailCnt + 1);
            var isLocked = verification.FailCnt >= PhoneVerificationMaxFailCount;
            if (isLocked)
                verification.LockUntilDt = thisTime.AddMinutes(PhoneVerificationLockMinutes);
            verification.UpdateDt = thisTime;
            verification.UpdateUser = AuditUser;

            if (!await _authRepository.UpdatePhoneVerificationAsync(verification, cancellationToken).ConfigureAwait(false))
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return new ResponseModel<TokenAuthResultDto>
                    { message = "서버에서 요청을 처리하지 못하였습니다.", data = null, code = 500 };
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            return isLocked
                ? TokenError("otp_locked", "verification is temporarily locked",
                    retryAfter: PhoneVerificationLockMinutes * 60)
                : TokenError("otp_mismatch", "verification code is incorrect",
                    remainingAttempts: PhoneVerificationMaxFailCount - verification.FailCnt);
        }

        // 6) 인증 성공 기록
        verification.VerifyDt = thisTime;
        verification.UpdateDt = thisTime;
        verification.UpdateUser = AuditUser;
        if (!await _authRepository.UpdatePhoneVerificationAsync(verification, cancellationToken).ConfigureAwait(false))
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return new ResponseModel<TokenAuthResultDto>
                { message = "서버에서 요청을 처리하지 못하였습니다.", data = null, code = 500 };
        }

        // 7) 계정 확인 — 가입은 휴대폰 웹에서 처리한다
        var userId = await _authRepository.GetUserIdByPhoneHashAsync(phoneHash,
            transaction.GetDbTransaction(), cancellationToken).ConfigureAwait(false);
        if (userId is null)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return TokenError("signup_required", "account does not exist");
        }

        // 8) 같은 기기의 기존 차량 세션 종료
        // 잠그지 않는 조회라도 안전하다: 같은 계정의 차량 세션을 만드는 경로는 지금 otp뿐이고, 같은 번호의 인증번호 행 잠금으로 직렬화된다.
        // 세션을 만드는 경로(QR 로그인 등)가 늘면 USER_INFO 행을 PK로 FOR UPDATE하는 공통 잠금으로 옮긴다
        var oldSessionIds = await _authRepository.GetCarSessionIdsAsync(userId.Value, deviceId,
            transaction.GetDbTransaction(), cancellationToken).ConfigureAwait(false);
        await _authRepository.EndLoginSessionsAsync(oldSessionIds, thisTime, AuditUser, cancellationToken).ConfigureAwait(false);

        // 9) 새 차량 세션 저장
        var refreshToken = _tokenService.CreateRefreshToken();
        var session = new LoginSession
        {
            UserId = userId.Value,
            SessionType = "CAR",
            TokenHash = _hashService.Hmac(refreshToken),
            DeviceId = deviceId,
            ExpireDt = thisTime.AddDays(ITokenService.RefreshTokenDays),
            LastUsedDt = thisTime,
            CreateDt = thisTime,
            CreateUser = AuditUser,
            UpdateDt = thisTime,
            UpdateUser = AuditUser,
            DeleteYn = false
        };

        if (!await _authRepository.AddLoginSessionAsync(session, cancellationToken).ConfigureAwait(false))
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return new ResponseModel<TokenAuthResultDto>
                { message = "서버에서 요청을 처리하지 못하였습니다.", data = null, code = 500 };
        }

        // 10) 초기 설정 필요 여부 확인
        var setupRequired = await _authRepository.GetSetupRequiredAsync(userId.Value,
            transaction.GetDbTransaction(), cancellationToken).ConfigureAwait(false);
        if (setupRequired is null)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return TokenError("invalid_grant", "account is invalid");
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        // 11) 응답 — 커밋한 뒤에만 토큰을 내준다
        var result = new TokenAuthResultDto
        {
            success = new TokenAuthSuccessDto
            {
                access_token = _tokenService.CreateAccessToken(userId.Value, session.LoginSessionId, deviceId, thisTime),
                token_type = "Bearer",
                expires_in = ITokenService.AccessTokenSeconds,
                refresh_token = refreshToken,
                session_id = session.LoginSessionId,
                setup_required = setupRequired.Value
            }
        };

        return new ResponseModel<TokenAuthResultDto> { message = "요청이 정상 처리되었습니다.", data = result, code = 200 };
    }

    private string CreatePhoneHash(string phoneDigits)
    {
        return _hashService.Hmac(phoneDigits);
    }

    private string CreateCodeHash(string phoneDigits, string code)
    {
        return _hashService.Hmac("otp:" + phoneDigits + ":" + code);
    }

    private static bool IsValidPhone(string? phone)
    {
        return phone is not null && Regex.IsMatch(phone, @"\A01[016789][0-9]{7,8}\z");
    }

    private static bool IsValidCode(string? code)
    {
        return code is not null && Regex.IsMatch(code, @"\A[0-9]{6}\z");
    }

    // 교착(MySQL 1213)이면 변경 추적을 비우고 짧게 기다린 뒤 트랜잭션 전체를 다시 실행한다(최대 3번 실행). 세 번 모두 교착이면 500
    private async Task<ResponseModel<T>> RetryOnDeadlockAsync<T>(Func<Task<ResponseModel<T>>> work)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await work().ConfigureAwait(false);
            }
            catch (Exception ex) when (IsDeadlock(ex))
            {
                _context.ChangeTracker.Clear();
                if (attempt < DeadlockMaxAttempts)
                {
                    // 동시에 진 요청들이 같은 순간에 다시 갭 락을 잡지 않도록 무작위로 기다린다
                    await Task.Delay(Random.Shared.Next(10, 50)).ConfigureAwait(false);
                    continue;
                }

                _logService.LogMessage(ex.ToString());
#if DEBUG
                _logService.ConsoleWarning(ex.ToString());
#endif
                return new ResponseModel<T> { message = "서버에서 요청을 처리하지 못하였습니다.", data = default, code = 500 };
            }
        }
    }

    private static bool IsDeadlock(Exception? ex)
    {
        for (; ex is not null; ex = ex.InnerException)
        {
            if (ex is MySqlException { Number: 1213 })
                return true;
        }

        return false;
    }

    private static DateTime NowToSecond()
    {
        var now = DateTime.Now;
        return now.AddTicks(-(now.Ticks % TimeSpan.TicksPerSecond));
    }

    private static int RemainingSeconds(DateTime until, DateTime thisTime)
    {
        return (int)Math.Ceiling((until - thisTime).TotalSeconds);
    }

    // OAuth 실패 응답(400). 설명에는 값이나 내부 사정을 넣지 않는다
    private static ResponseModel<TokenAuthResultDto> TokenError(string error, string errorDescription,
        int? remainingAttempts = null, int? retryAfter = null)
    {
        return new ResponseModel<TokenAuthResultDto>
        {
            message = "잘못된 요청입니다.",
            data = new TokenAuthResultDto
            {
                error = new TokenAuthErrorDto
                {
                    error = error,
                    error_description = errorDescription,
                    remaining_attempts = remainingAttempts,
                    retry_after = retryAfter
                }
            },
            code = 400
        };
    }
}
