using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore.Storage;
using MySql.Data.MySqlClient;
using RideMateService.Comm;
using RideMateService.Comm.Dto.v1.User.Request;
using RideMateService.Comm.Dto.v1.User.Response;
using RideMateService.Comm.Logger;
using RideMateService.Comm.Security;
using RideMateService.Commons.Repository;
using RideMateService.Repository.v1.User;

namespace RideMateService.Service.v1.User;

public class UserService : IUserService
{
    private const string AuditUser = "PARENT";
    private const int AddressMaxLength = 200;
    private const int CarModelMaxLength = 20;
    private const int DeadlockMaxAttempts = 3;

    private static readonly string[] CarColors = { "WHITE", "BLACK", "GRAY", "ETC" };

    private readonly IUserRepository _userRepository;
    private readonly ILogService _logService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly RideMateContext _context; // 트랜잭션 시작·완료에만 쓴다
    private readonly IPhoneCryptoService _phoneCryptoService;

    public UserService(IUserRepository userRepository,
        ILogService logService,
        IHttpContextAccessor httpContextAccessor,
        RideMateContext context,
        IPhoneCryptoService phoneCryptoService)
    {
        this._userRepository = userRepository;
        this._logService = logService;
        this._httpContextAccessor = httpContextAccessor;
        this._context = context;
        this._phoneCryptoService = phoneCryptoService;
    }

    /// <summary>
    /// 집 저장
    /// </summary>
    public Task<ResponseModel<HomeDto>> UpdateHomeService(UpdateHomeDto dto,
        CancellationToken cancellationToken = default)
    {
        return RetryOnDeadlockAsync(() => UpdateHomeCoreAsync(dto, cancellationToken), cancellationToken);
    }

    // 교착은 RetryOnDeadlockAsync가 다시 실행하므로 여기서는 500으로 바꾸지 않고 올려보낸다
    private async Task<ResponseModel<HomeDto>> UpdateHomeCoreAsync(UpdateHomeDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            // 1) 입력 검증 — API 모델 검증을 거치지 않은 호출도 같은 규칙으로 거절한다
            var address = dto?.address?.Trim();
            if (dto is null || address is null || !IsLengthBetween(address, 1, AddressMaxLength)
                || !IsValidCoordinate(dto.lat, dto.lng))
                return Fail<HomeDto>(400, "잘못된 요청입니다.");

            // 2) 인증 확인 — sub 클레임이 없으면 거절하고, 계정이 있는지는 아래 잠금 조회가 확인한다
            var loginUserId = GetLoginUserId();
            if (loginUserId is null)
                return Fail<HomeDto>(401, "잘못된 요청입니다.");

            DateTime thisTime = NowToSecond();
            var transaction = await _context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await using var transactionScope = transaction.ConfigureAwait(false);
            var dbTransaction = transaction.GetDbTransaction();

            // 3) 다른 조회보다 먼저 계정을 잠가 같은 계정의 동시 요청을 직렬화한다
            var lockedUserId = await _userRepository.LockUserInfoAsync(loginUserId.Value, dbTransaction,
                cancellationToken).ConfigureAwait(false);
            if (lockedUserId is null) // 계정이 없거나 지워졌다
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return Fail<HomeDto>(401, "잘못된 요청입니다.");
            }

            // 4) 집 주소·좌표만 바꾼다 — 좌표가 안 오면 NULL로 바꿔 주소와 다른 옛 좌표가 남지 않게 한다
            if (!await _userRepository.UpdateHomeAsync(lockedUserId.Value, address, dto.lat, dto.lng, thisTime,
                    AuditUser, cancellationToken).ConfigureAwait(false))
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return Fail<HomeDto>(500, "서버에서 요청을 처리하지 못하였습니다.");
            }

            // 5) 응답 — 저장된 값을 읽는다(좌표는 DB 자릿수로 맞춰진다). 커밋 전에 읽어 읽기가 실패하면 저장도 취소된다
            var row = await _userRepository.GetUserSetupRowAsync(lockedUserId.Value, dbTransaction,
                cancellationToken).ConfigureAwait(false);
            if (row?.DestAddress is null)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return Fail<HomeDto>(500, "서버에서 요청을 처리하지 못하였습니다.");
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            return new ResponseModel<HomeDto>
            {
                message = "요청이 정상 처리되었습니다.",
                data = new HomeDto { address = row.DestAddress, lat = row.DestLat, lng = row.DestLng },
                code = 200
            };
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested && !IsDeadlock(ex)) // 요청 취소·교착은 그대로 올려보낸다
        {
            LogServiceException(ex);
            return Fail<HomeDto>(500, "서버에서 요청을 처리하지 못하였습니다.");
        }
    }

    /// <summary>
    /// 차 정보 저장
    /// </summary>
    public Task<ResponseModel<VehicleDto>> UpdateVehicleService(UpdateVehicleDto dto,
        CancellationToken cancellationToken = default)
    {
        return RetryOnDeadlockAsync(() => UpdateVehicleCoreAsync(dto, cancellationToken), cancellationToken);
    }

    // 교착은 RetryOnDeadlockAsync가 다시 실행하므로 여기서는 500으로 바꾸지 않고 올려보낸다
    private async Task<ResponseModel<VehicleDto>> UpdateVehicleCoreAsync(UpdateVehicleDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            // 1) 입력 검증 — null은 "바꾸지 않음"이라 통과하고, 값이 온 필드만 검사한다. 빈 문자열은 값이 온 것이므로 거절한다
            if (dto is null)
                return Fail<VehicleDto>(400, "잘못된 요청입니다.");

            var carModel = dto.carModel?.Trim();
            if (carModel is not null && !IsLengthBetween(carModel, 1, CarModelMaxLength))
                return Fail<VehicleDto>(400, "잘못된 요청입니다.");
            if (dto.carColor is not null && !CarColors.Contains(dto.carColor))
                return Fail<VehicleDto>(400, "잘못된 요청입니다.");
            if (dto.carNoLast4 is not null && !Regex.IsMatch(dto.carNoLast4, @"\A[0-9]{4}\z"))
                return Fail<VehicleDto>(400, "잘못된 요청입니다.");

            // 2) 인증 확인 — sub 클레임이 없으면 거절하고, 계정이 있는지는 아래 잠금 조회가 확인한다
            var loginUserId = GetLoginUserId();
            if (loginUserId is null)
                return Fail<VehicleDto>(401, "잘못된 요청입니다.");

            DateTime thisTime = NowToSecond();
            var transaction = await _context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await using var transactionScope = transaction.ConfigureAwait(false);
            var dbTransaction = transaction.GetDbTransaction();

            // 3) 다른 조회보다 먼저 계정을 잠가 같은 계정의 동시 요청을 직렬화한다
            var lockedUserId = await _userRepository.LockUserInfoAsync(loginUserId.Value, dbTransaction,
                cancellationToken).ConfigureAwait(false);
            if (lockedUserId is null) // 계정이 없거나 지워졌다
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return Fail<VehicleDto>(401, "잘못된 요청입니다.");
            }

            // 4) 값이 온 칸만 바꾼다. 셋 다 안 왔으면 바꿀 것이 없으니 현재 값을 돌려준다
            if ((carModel is not null || dto.carColor is not null || dto.carNoLast4 is not null)
                && !await _userRepository.UpdateVehicleAsync(lockedUserId.Value, carModel, dto.carColor,
                    dto.carNoLast4, thisTime, AuditUser, cancellationToken).ConfigureAwait(false))
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return Fail<VehicleDto>(500, "서버에서 요청을 처리하지 못하였습니다.");
            }

            // 5) 응답 — 저장된 현재 값을 읽는다. 커밋 전에 읽어 읽기가 실패하면 저장도 취소된다
            var row = await _userRepository.GetUserSetupRowAsync(lockedUserId.Value, dbTransaction,
                cancellationToken).ConfigureAwait(false);
            if (row is null)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return Fail<VehicleDto>(500, "서버에서 요청을 처리하지 못하였습니다.");
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            return new ResponseModel<VehicleDto>
            {
                message = "요청이 정상 처리되었습니다.",
                data = new VehicleDto { carModel = row.CarModel, carColor = row.CarColor, carNoLast4 = row.CarNoLast4 },
                code = 200
            };
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested && !IsDeadlock(ex)) // 요청 취소·교착은 그대로 올려보낸다
        {
            LogServiceException(ex);
            return Fail<VehicleDto>(500, "서버에서 요청을 처리하지 못하였습니다.");
        }
    }

    /// <summary>
    /// 초기 설정 완료
    /// </summary>
    public Task<ResponseModel<SetupCompleteDto>> UpdateSetupCompleteService(
        CancellationToken cancellationToken = default)
    {
        return RetryOnDeadlockAsync(() => UpdateSetupCompleteCoreAsync(cancellationToken), cancellationToken);
    }

    // 교착은 RetryOnDeadlockAsync가 다시 실행하므로 여기서는 500으로 바꾸지 않고 올려보낸다
    private async Task<ResponseModel<SetupCompleteDto>> UpdateSetupCompleteCoreAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            // 1) 인증 확인 — sub 클레임이 없으면 거절하고, 계정이 있는지는 아래 잠금 조회가 확인한다
            var loginUserId = GetLoginUserId();
            if (loginUserId is null)
                return Fail<SetupCompleteDto>(401, "잘못된 요청입니다.");

            DateTime thisTime = NowToSecond();
            var transaction = await _context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await using var transactionScope = transaction.ConfigureAwait(false);
            var dbTransaction = transaction.GetDbTransaction();

            // 2) 다른 조회보다 먼저 계정을 잠가 같은 계정의 동시 요청을 직렬화한다 — 잠근 뒤에 일반 조회를 해야 직전 커밋을 본다
            var lockedUserId = await _userRepository.LockUserInfoAsync(loginUserId.Value, dbTransaction,
                cancellationToken).ConfigureAwait(false);
            if (lockedUserId is null) // 계정이 없거나 지워졌다
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return Fail<SetupCompleteDto>(401, "잘못된 요청입니다.");
            }

            // 3) 현재 값을 읽는다. 이미 완료한 계정은 조건을 다시 보지 않고 처음 시각 그대로 200이다 — 이후 아이·시간표를 지워도 초기 설정을 다시 강제하지 않는다
            var row = await _userRepository.GetUserSetupRowAsync(lockedUserId.Value, dbTransaction,
                cancellationToken).ConfigureAwait(false);
            if (row is null)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return Fail<SetupCompleteDto>(500, "서버에서 요청을 처리하지 못하였습니다.");
            }

            if (row.SetupDoneDt is not null)
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return new ResponseModel<SetupCompleteDto>
                {
                    message = "요청이 정상 처리되었습니다.",
                    data = new SetupCompleteDto { setupDoneDt = row.SetupDoneDt.Value },
                    code = 200
                };
            }

            // 4) 살아 있는 아이가 없으면 아이 정보부터 등록하게 한다
            if (!await _userRepository.HasLiveChildAsync(lockedUserId.Value, dbTransaction,
                    cancellationToken).ConfigureAwait(false))
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return Fail<SetupCompleteDto>(101, "아이 정보를 먼저 등록해 주세요.");
            }

            // 5) 픽업이 켜진 시간표 요일이 하나도 없으면 거절한다 — 시간표가 있어도 스위치가 모두 꺼져 있으면 같다
            if (!await _userRepository.HasPickupOnScheduleAsync(lockedUserId.Value, dbTransaction,
                    cancellationToken).ConfigureAwait(false))
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return Fail<SetupCompleteDto>(102, "학원을 하나 이상 추가하고 픽업을 켜 주세요");
            }

            // 6) 집 주소가 없으면 거절한다
            if (string.IsNullOrWhiteSpace(row.DestAddress))
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return Fail<SetupCompleteDto>(103, "집 위치를 확인해 주세요");
            }

            // 7) 완료 시각을 채운다 — 위에서 비어 있음을 잠근 뒤에 확인했으므로 반드시 한 행이 바뀐다
            if (!await _userRepository.UpdateSetupDoneAsync(lockedUserId.Value, thisTime, AuditUser,
                    cancellationToken).ConfigureAwait(false))
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return Fail<SetupCompleteDto>(500, "서버에서 요청을 처리하지 못하였습니다.");
            }

            // 응답 — 저장된 현재 값을 읽는다. 커밋 전에 읽어 읽기가 실패하면 저장도 취소된다
            row = await _userRepository.GetUserSetupRowAsync(lockedUserId.Value, dbTransaction,
                cancellationToken).ConfigureAwait(false);
            if (row?.SetupDoneDt is null)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return Fail<SetupCompleteDto>(500, "서버에서 요청을 처리하지 못하였습니다.");
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            return new ResponseModel<SetupCompleteDto>
            {
                message = "요청이 정상 처리되었습니다.",
                data = new SetupCompleteDto { setupDoneDt = row.SetupDoneDt.Value },
                code = 200
            };
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested && !IsDeadlock(ex)) // 요청 취소·교착은 그대로 올려보낸다
        {
            LogServiceException(ex);
            return Fail<SetupCompleteDto>(500, "서버에서 요청을 처리하지 못하였습니다.");
        }
    }

    /// <summary>
    /// 자동 발송 설정 저장
    /// </summary>
    public Task<ResponseModel<SettingDto>> UpdateSettingService(UpdateSettingDto dto,
        CancellationToken cancellationToken = default)
    {
        return RetryOnDeadlockAsync(() => UpdateSettingCoreAsync(dto, cancellationToken), cancellationToken);
    }

    // 교착은 RetryOnDeadlockAsync가 다시 실행하므로 여기서는 500으로 바꾸지 않고 올려보낸다
    private async Task<ResponseModel<SettingDto>> UpdateSettingCoreAsync(UpdateSettingDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            // 1) 입력 검증 — API 모델 검증을 거치지 않은 호출도 같은 규칙으로 거절한다
            if (dto?.autoSendYn is null)
                return Fail<SettingDto>(400, "잘못된 요청입니다.");

            // 2) 인증 확인 — sub 클레임이 없으면 거절하고, 계정이 있는지는 아래 잠금 조회가 확인한다
            var loginUserId = GetLoginUserId();
            if (loginUserId is null)
                return Fail<SettingDto>(401, "잘못된 요청입니다.");

            DateTime thisTime = NowToSecond();
            var transaction = await _context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await using var transactionScope = transaction.ConfigureAwait(false);
            var dbTransaction = transaction.GetDbTransaction();

            // 3) 다른 조회보다 먼저 계정을 잠가 같은 계정의 동시 요청을 직렬화한다
            var lockedUserId = await _userRepository.LockUserInfoAsync(loginUserId.Value, dbTransaction,
                cancellationToken).ConfigureAwait(false);
            if (lockedUserId is null) // 계정이 없거나 지워졌다
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return Fail<SettingDto>(401, "잘못된 요청입니다.");
            }

            // 4) 자동 발송 여부만 바꾼다
            if (!await _userRepository.UpdateAutoSendAsync(lockedUserId.Value, dto.autoSendYn.Value, thisTime,
                    AuditUser, cancellationToken).ConfigureAwait(false))
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return Fail<SettingDto>(500, "서버에서 요청을 처리하지 못하였습니다.");
            }

            // 5) 응답 — 저장된 값을 읽는다. 커밋 전에 읽어 읽기가 실패하면 저장도 취소된다
            var row = await _userRepository.GetUserSetupRowAsync(lockedUserId.Value, dbTransaction,
                cancellationToken).ConfigureAwait(false);
            if (row is null)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return Fail<SettingDto>(500, "서버에서 요청을 처리하지 못하였습니다.");
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            return new ResponseModel<SettingDto>
            {
                message = "요청이 정상 처리되었습니다.",
                data = new SettingDto { autoSendYn = row.AutoSendYn },
                code = 200
            };
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested && !IsDeadlock(ex)) // 요청 취소·교착은 그대로 올려보낸다
        {
            LogServiceException(ex);
            return Fail<SettingDto>(500, "서버에서 요청을 처리하지 못하였습니다.");
        }
    }

    /// <summary>
    /// 계정 정보 조회
    /// </summary>
    public async Task<ResponseModel<DetailUserDto>> DetailUserService(CancellationToken cancellationToken = default)
    {
        try
        {
            // 1) 인증 확인 — sub 클레임이 없으면 거절한다
            var loginUserId = GetLoginUserId();
            if (loginUserId is null)
                return Fail<DetailUserDto>(401, "잘못된 요청입니다.");

            // 2) 계정·집·자동 발송 여부·아이를 한 번에 읽는다
            var row = await _userRepository.GetUserDetailRowAsync(loginUserId.Value,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            if (row is null) // 계정이 없거나 지워졌다
                return Fail<DetailUserDto>(401, "잘못된 요청입니다.");

            return new ResponseModel<DetailUserDto>
            {
                message = "요청이 정상 처리되었습니다.",
                data = new DetailUserDto
                {
                    child = row.ChildId is null
                        ? null
                        : new DetailUserChildDto
                        {
                            childSeq = row.ChildId.Value,
                            childName = row.ChildName ?? string.Empty,
                            phoneMasked = MaskStoredPhone(row.PhoneEnc),
                            sendChannel = row.SendChannel ?? string.Empty
                        },
                    home = row.DestAddress is null
                        ? null
                        : new HomeDto { address = row.DestAddress, lat = row.DestLat, lng = row.DestLng },
                    autoSendYn = row.AutoSendYn
                },
                code = 200
            };
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested) // 요청 취소는 그대로 올려보낸다
        {
            LogServiceException(ex);
            return Fail<DetailUserDto>(500, "서버에서 요청을 처리하지 못하였습니다.");
        }
    }

    // 저장된 번호를 복호화해 가린다. 복호화할 수 없으면 조회를 막지 않도록 빈 문자열을 돌려준다
    private string MaskStoredPhone(byte[]? phoneEnc)
    {
        if (phoneEnc is null || phoneEnc.Length == 0)
            return string.Empty;

        try
        {
            var phoneDigits = _phoneCryptoService.Decrypt(phoneEnc);
            return phoneDigits[..3] + "-****-" + phoneDigits[^4..];
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    // 로그인한 계정(JWT sub)의 ID — 클레임이 없거나 숫자가 아니면 null. 부른 쪽이 401로 바꾼다
    private int? GetLoginUserId()
    {
        return int.TryParse(_httpContextAccessor.HttpContext?.User.FindFirstValue("sub"), out var userId)
            ? userId
            : null;
    }

    // 좌표: 둘 다 있거나 둘 다 없어야 하고, 위도 -90~90, 경도 -180~180
    private static bool IsValidCoordinate(decimal? lat, decimal? lng)
    {
        if ((lat is null) != (lng is null))
            return false;

        return lat is null || (lat >= -90m && lat <= 90m && lng >= -180m && lng <= 180m);
    }

    // 앞뒤 공백을 지운 값이 min~max자(코드 포인트)인지 본다
    private static bool IsLengthBetween(string trimmed, int min, int max)
    {
        var length = trimmed.EnumerateRunes().Count();
        return length >= min && length <= max;
    }

    private static ResponseModel<T> Fail<T>(int code, string message)
    {
        return new ResponseModel<T> { message = message, data = default, code = code };
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
