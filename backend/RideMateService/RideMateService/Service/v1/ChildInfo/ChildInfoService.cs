using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore.Storage;
using MySql.Data.MySqlClient;
using RideMateService.Comm;
using RideMateService.Comm.Dto.v1.ChildInfo.Request;
using RideMateService.Comm.Dto.v1.ChildInfo.Response;
using RideMateService.Comm.Logger;
using RideMateService.Comm.Security;
using RideMateService.Commons.DBModels;
using RideMateService.Commons.Repository;
using RideMateService.Repository.v1.ChildInfo;

namespace RideMateService.Service.v1.ChildInfo;

public class ChildInfoService : IChildInfoService
{
    private const string AuditUser = "PARENT";
    private const string ConsentTypeChildInfo = "CHILD_INFO";
    private const int ChildNameMaxLength = 10;
    private const int DeadlockMaxAttempts = 3;

    private readonly IChildInfoRepository _childInfoRepository;
    private readonly ILogService _logService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly RideMateContext _context; // 트랜잭션 시작·완료에만 쓴다
    private readonly IHashService _hashService;
    private readonly IPhoneCryptoService _phoneCryptoService;

    public ChildInfoService(IChildInfoRepository childInfoRepository,
        ILogService logService,
        IHttpContextAccessor httpContextAccessor,
        RideMateContext context,
        IHashService hashService,
        IPhoneCryptoService phoneCryptoService)
    {
        this._childInfoRepository = childInfoRepository;
        this._logService = logService;
        this._httpContextAccessor = httpContextAccessor;
        this._context = context;
        this._hashService = hashService;
        this._phoneCryptoService = phoneCryptoService;
    }

    /// <summary>
    /// 아이 정보 저장
    /// </summary>
    public Task<ResponseModel<AddChildResultDto>> AddChildService(AddChildDto dto,
        CancellationToken cancellationToken = default)
    {
        return RetryOnDeadlockAsync(() => AddChildCoreAsync(dto, cancellationToken), cancellationToken);
    }

    // 교착은 RetryOnDeadlockAsync가 다시 실행하므로 여기서는 500으로 바꾸지 않고 올려보낸다
    private async Task<ResponseModel<AddChildResultDto>> AddChildCoreAsync(AddChildDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            // 1) 입력 검증 — API 모델 검증을 거치지 않은 호출도 같은 규칙으로 거절한다
            var childName = dto?.childName?.Trim();
            if (dto is null || childName is null || !IsValidChildName(childName)
                || !IsValidPhone(dto.phone) || !IsValidSendChannel(dto.sendChannel))
                return Fail(400, "잘못된 요청입니다.");

            // 2) 인증 확인 — 번호 비교에 쓸 부모 번호 해시가 필요해 트랜잭션 전에 읽는다
            var loginUser = await GetLoginUserAsync(cancellationToken).ConfigureAwait(false);
            if (loginUser is null)
                return Fail(401, "잘못된 요청입니다.");

            DateTime thisTime = NowToSecond();
            var phoneDigits = dto.phone!;
            var phoneHash = _hashService.Hmac(phoneDigits);
            var phoneMasked = MaskPhone(phoneDigits); // 커밋 뒤에 실패하지 않도록 미리 만든다
            var transaction = await _context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await using var transactionScope = transaction.ConfigureAwait(false);

            // 3) 다른 조회보다 먼저 계정을 잠가 같은 계정의 동시 요청을 직렬화한다
            var lockedUserId = await _childInfoRepository.LockUserInfoAsync(loginUser.UserId,
                transaction.GetDbTransaction(), cancellationToken).ConfigureAwait(false);
            if (lockedUserId is null) // 잠그는 사이에 계정이 지워졌다
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return Fail(401, "잘못된 요청입니다.");
            }

            // 4) 가장 최근 아이 정보 동의가 동의(AGREE_YN = 1)인지 본다
            var childInfoAgree = await _childInfoRepository.GetLatestConsentAgreeAsync(lockedUserId.Value,
                ConsentTypeChildInfo, transaction.GetDbTransaction(), cancellationToken).ConfigureAwait(false);
            if (childInfoAgree != true)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return Fail(101, "필수 항목에 동의해 주세요.");
            }

            // 5) 부모 번호와 같은지 해시로 비교한다 — 같은 HMAC 함수라 복호화가 필요 없다
            if (string.Equals(phoneHash, loginUser.PhoneHash, StringComparison.Ordinal))
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return Fail(102, "아이 번호를 입력해 주세요. 부모님 번호와 같아요");
            }

            // 6) 이 계정에 살아 있는 아이가 이미 있으면 거절하되, 앱이 "이미 저장됨"을 보여 주도록 그 아이를 담아 돌려준다
            var existingChild = await _childInfoRepository.GetLiveChildAsync(lockedUserId.Value,
                transaction.GetDbTransaction(), cancellationToken).ConfigureAwait(false);
            if (existingChild is not null)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return new ResponseModel<AddChildResultDto>
                {
                    message = "이미 등록된 아이가 있습니다.",
                    data = new AddChildResultDto
                    {
                        childSeq = existingChild.ChildId,
                        childName = existingChild.ChildName,
                        phoneMasked = MaskStoredPhone(existingChild.PhoneEnc),
                        sendChannel = existingChild.SendChannel
                    },
                    code = 409
                };
            }

            // 7) 다른 계정의 살아 있는 아이와 번호가 같으면 미리 거절한다 — 정상 거절 경로가 중복 키 예외를 거치지 않게 한다
            if (await _childInfoRepository.ExistsLivePhoneHashAsync(phoneHash, transaction.GetDbTransaction(),
                    cancellationToken).ConfigureAwait(false))
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return Fail(103, "이미 다른 계정에 등록된 번호예요.");
            }

            // 8) 저장 — 감사 컬럼은 Service가 채운다
            var child = new Child
            {
                UserId = lockedUserId.Value,
                ChildName = childName,
                PhoneEnc = _phoneCryptoService.Encrypt(phoneDigits),
                PhoneHash = phoneHash,
                SendChannel = dto.sendChannel!,
                CreateDt = thisTime,
                CreateUser = AuditUser,
                UpdateDt = thisTime,
                UpdateUser = AuditUser,
                DeleteYn = false
            };

            try
            {
                if (!await _childInfoRepository.AddChildAsync(child, cancellationToken).ConfigureAwait(false))
                {
                    await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    return Fail(500, "서버에서 요청을 처리하지 못하였습니다.");
                }
            }
            catch (Exception ex) when (IsDuplicateKey(ex)) // 7)을 지난 뒤 다른 계정이 먼저 같은 번호를 커밋한 경합(UK_CHILD_01)
            {
                _context.ChangeTracker.Clear();
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return Fail(103, "이미 다른 계정에 등록된 번호예요.");
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            // 9) 응답
            return new ResponseModel<AddChildResultDto>
            {
                message = "요청이 정상 처리되었습니다.",
                data = new AddChildResultDto
                {
                    childSeq = child.ChildId,
                    childName = child.ChildName,
                    phoneMasked = phoneMasked,
                    sendChannel = child.SendChannel
                },
                code = 200
            };
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested && !IsDeadlock(ex)) // 요청 취소·교착은 그대로 올려보낸다
        {
            LogServiceException(ex);
            return Fail(500, "서버에서 요청을 처리하지 못하였습니다.");
        }
    }

    /// <summary>
    /// 아이 정보 수정
    /// </summary>
    public Task<ResponseModel<AddChildResultDto>> UpdateChildService(UpdateChildDto dto,
        CancellationToken cancellationToken = default)
    {
        return RetryOnDeadlockAsync(() => UpdateChildCoreAsync(dto, cancellationToken), cancellationToken);
    }

    // 교착은 RetryOnDeadlockAsync가 다시 실행하므로 여기서는 500으로 바꾸지 않고 올려보낸다
    private async Task<ResponseModel<AddChildResultDto>> UpdateChildCoreAsync(UpdateChildDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            // 1) 입력 검증 — 보낸 필드만 형식을 보고, 셋 다 없으면 바꿀 것이 없어 거절한다
            if (dto?.childSeq is null)
                return Fail(400, "잘못된 요청입니다.");

            var newName = dto.childName?.Trim();
            if ((dto.childName is null && dto.phone is null && dto.sendChannel is null)
                || (newName is not null && !IsValidChildName(newName))
                || (dto.phone is not null && !IsValidPhone(dto.phone))
                || (dto.sendChannel is not null && !IsValidSendChannel(dto.sendChannel)))
                return Fail(400, "잘못된 요청입니다.");

            // 2) 인증 확인 — 번호 비교에 쓸 부모 번호 해시가 필요해 트랜잭션 전에 읽는다
            var loginUser = await GetLoginUserAsync(cancellationToken).ConfigureAwait(false);
            if (loginUser is null)
                return Fail(401, "잘못된 요청입니다.");

            DateTime thisTime = NowToSecond();
            var newPhoneHash = dto.phone is null ? null : _hashService.Hmac(dto.phone);
            var transaction = await _context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await using var transactionScope = transaction.ConfigureAwait(false);

            // 3) 다른 조회보다 먼저 계정을 잠가 같은 계정의 동시 요청을 직렬화한다(AddChild와 같은 순서)
            var lockedUserId = await _childInfoRepository.LockUserInfoAsync(loginUser.UserId,
                transaction.GetDbTransaction(), cancellationToken).ConfigureAwait(false);
            if (lockedUserId is null) // 잠그는 사이에 계정이 지워졌다
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return Fail(401, "잘못된 요청입니다.");
            }

            // 4) 가장 최근 아이 정보 동의가 동의(AGREE_YN = 1)인지 본다 — 동의를 철회했다면 수정도 막는다
            var childInfoAgree = await _childInfoRepository.GetLatestConsentAgreeAsync(lockedUserId.Value,
                ConsentTypeChildInfo, transaction.GetDbTransaction(), cancellationToken).ConfigureAwait(false);
            if (childInfoAgree != true)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return Fail(101, "필수 항목에 동의해 주세요.");
            }

            // 5) 이 계정의 살아 있는 아이가 요청한 아이인지 본다 — 없거나 다른 아이면 404
            var child = await _childInfoRepository.GetLiveChildAsync(lockedUserId.Value,
                transaction.GetDbTransaction(), cancellationToken).ConfigureAwait(false);
            if (child is null || child.ChildId != dto.childSeq.Value)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return Fail(404, "잘못된 요청입니다.");
            }

            // 6) 바뀐 것만 가려낸다 — 지금 번호와 같은 번호는 바꾸지 않은 것이라 102·103을 보지 않는다
            var phoneChanged = dto.phone is not null && IsPhoneChanged(child, dto.phone, newPhoneHash!);
            var nameChanged = newName is not null && !string.Equals(newName, child.ChildName, StringComparison.Ordinal);
            var channelChanged = dto.sendChannel is not null
                                 && !string.Equals(dto.sendChannel, child.SendChannel, StringComparison.Ordinal);

            var storedName = nameChanged ? newName! : child.ChildName;
            var storedChannel = channelChanged ? dto.sendChannel! : child.SendChannel;
            var phoneMasked = phoneChanged ? MaskPhone(dto.phone!) : MaskStoredPhone(child.PhoneEnc); // 커밋 뒤에 실패하지 않도록 미리 만든다

            if (!phoneChanged && !nameChanged && !channelChanged)
            {
                // 바꿀 것이 없다 — 쓰지 않고(UPDATE_DT·UPDATE_USER도 그대로) 저장된 값을 돌려준다
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return OkResult(child.ChildId, storedName, phoneMasked, storedChannel);
            }

            if (phoneChanged)
            {
                // 부모 번호와 같은지 해시로 비교한다
                if (string.Equals(newPhoneHash, loginUser.PhoneHash, StringComparison.Ordinal))
                {
                    await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    return Fail(102, "아이 번호를 입력해 주세요. 부모님 번호와 같아요");
                }

                // 다른 계정의 살아 있는 아이와 번호가 같으면 미리 거절한다 — 정상 거절 경로가 중복 키 예외를 거치지 않게 한다
                if (await _childInfoRepository.ExistsLivePhoneHashAsync(newPhoneHash!, transaction.GetDbTransaction(),
                        cancellationToken).ConfigureAwait(false))
                {
                    await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    return Fail(103, "이미 다른 계정에 등록된 번호예요.");
                }
            }

            // 7) 저장 — 바뀐 칸만 쓰고, 번호를 바꾸면 암호문과 해시를 함께 바꾼다
            try
            {
                if (!await _childInfoRepository.UpdateChildAsync(child.ChildId,
                        nameChanged ? newName : null,
                        phoneChanged ? _phoneCryptoService.Encrypt(dto.phone!) : null,
                        phoneChanged ? newPhoneHash : null,
                        channelChanged ? dto.sendChannel : null,
                        thisTime, AuditUser, cancellationToken).ConfigureAwait(false))
                {
                    await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    return Fail(500, "서버에서 요청을 처리하지 못하였습니다.");
                }
            }
            catch (Exception ex) when (IsDuplicateKey(ex)) // 6)을 지난 뒤 다른 계정이 먼저 같은 번호를 커밋한 경합(UK_CHILD_01)
            {
                _context.ChangeTracker.Clear();
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return Fail(103, "이미 다른 계정에 등록된 번호예요.");
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            // 8) 응답 — 저장된 값을 돌려준다
            return OkResult(child.ChildId, storedName, phoneMasked, storedChannel);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested && !IsDeadlock(ex)) // 요청 취소·교착은 그대로 올려보낸다
        {
            LogServiceException(ex);
            return Fail(500, "서버에서 요청을 처리하지 못하였습니다.");
        }
    }

    private static ResponseModel<AddChildResultDto> OkResult(int childSeq, string childName, string phoneMasked,
        string sendChannel)
    {
        return new ResponseModel<AddChildResultDto>
        {
            message = "요청이 정상 처리되었습니다.",
            data = new AddChildResultDto
            {
                childSeq = childSeq,
                childName = childName,
                phoneMasked = phoneMasked,
                sendChannel = sendChannel
            },
            code = 200
        };
    }

    // 보낸 번호가 저장된 번호와 다른지 본다. 해시가 있으면 해시끼리, 해시가 NULL인 옛 행은 복호화한 번호와 비교한다(복호화 실패는 다른 번호로 본다)
    private bool IsPhoneChanged(Child child, string phoneDigits, string phoneHash)
    {
        if (child.PhoneHash is not null)
            return !string.Equals(phoneHash, child.PhoneHash, StringComparison.Ordinal);

        try
        {
            return !string.Equals(_phoneCryptoService.Decrypt(child.PhoneEnc), phoneDigits, StringComparison.Ordinal);
        }
        catch (Exception)
        {
            return true;
        }
    }

    // 로그인한 계정(JWT sub의 사용자) — 클레임이 없거나 계정이 없으면 null. 부른 쪽이 401로 바꾼다
    private async Task<UserInfo?> GetLoginUserAsync(CancellationToken cancellationToken)
    {
        if (!int.TryParse(_httpContextAccessor.HttpContext?.User.FindFirstValue("sub"), out var userId))
            return null;

        return await _childInfoRepository.GetUserInfoAsync(userId, cancellationToken).ConfigureAwait(false);
    }

    // 앞뒤 공백을 지운 이름이 1~10자(코드 포인트)이고 완성형 한글·영문만이며 단어 사이 공백이 한 칸인지 본다
    private static bool IsValidChildName(string trimmed)
    {
        var length = trimmed.EnumerateRunes().Count();
        if (length < 1 || length > ChildNameMaxLength)
            return false;

        return Regex.IsMatch(trimmed, @"\A[가-힣A-Za-z]+( [가-힣A-Za-z]+)*\z");
    }

    private static bool IsValidPhone(string? phone)
    {
        return phone is not null && Regex.IsMatch(phone, @"\A01[016789][0-9]{7,8}\z");
    }

    private static bool IsValidSendChannel(string? sendChannel)
    {
        return sendChannel is "SMS" or "ALIMTALK";
    }

    private static string MaskPhone(string phoneDigits)
    {
        return phoneDigits[..3] + "-****-" + phoneDigits[^4..];
    }

    // 저장된 번호를 복호화해 가린다. 복호화할 수 없는 옛 행이어도 409 응답을 막지 않도록 빈 문자열을 돌려준다
    private string MaskStoredPhone(byte[] phoneEnc)
    {
        try
        {
            return MaskPhone(_phoneCryptoService.Decrypt(phoneEnc));
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    private static ResponseModel<AddChildResultDto> Fail(int code, string message)
    {
        return new ResponseModel<AddChildResultDto> { message = message, data = null, code = code };
    }

    // 교착(MySQL 1213)이면 변경 추적을 비우고 트랜잭션 전체를 다시 실행한다(최대 3번 실행)
    private async Task<ResponseModel<T>> RetryOnDeadlockAsync<T>(Func<Task<ResponseModel<T>>> work,
        CancellationToken cancellationToken)
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
                    // 동시에 진 요청들이 같은 순간에 다시 잠금을 잡지 않도록 무작위로 기다린다
                    await Task.Delay(Random.Shared.Next(10, 50), cancellationToken).ConfigureAwait(false);
                    continue;
                }

                LogServiceException(ex);
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

    private static bool IsDuplicateKey(Exception? ex)
    {
        for (; ex is not null; ex = ex.InnerException)
        {
            if (ex is MySqlException { Number: 1062 })
                return true;
        }

        return false;
    }

    private static DateTime NowToSecond()
    {
        var now = DateTime.Now;
        return now.AddTicks(-(now.Ticks % TimeSpan.TicksPerSecond)); // DB DATETIME은 초 단위로 반올림해 저장하므로 기준 시각도 초 단위로 맞춘다
    }

    private void LogServiceException(Exception ex)
    {
        _logService.LogMessage(ex.ToString());
#if DEBUG
        _logService.ConsoleWarning(ex.ToString());
#endif
    }
}
