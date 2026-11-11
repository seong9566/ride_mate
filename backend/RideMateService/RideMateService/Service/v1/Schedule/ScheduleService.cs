using System.Data;
using System.Globalization;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore.Storage;
using MySql.Data.MySqlClient;
using RideMateService.Comm;
using RideMateService.Comm.Dto.v1.Schedule.Request;
using RideMateService.Comm.Dto.v1.Schedule.Response;
using RideMateService.Comm.Logger;
using RideMateService.Commons.DBModels;
using RideMateService.Commons.Repository;
using RideMateService.Repository.v1.Schedule;

namespace RideMateService.Service.v1.Schedule;

public class ScheduleService : IScheduleService
{
    private const string AuditUser = "PARENT";
    private const string AcademySourceTypePlace = "PLACE";
    private const int DisplayNameMaxLength = 20;
    private const int AcademyNameMaxLength = 100;
    private const int AddressMaxLength = 200;
    private const int MaxScheduleCount = 10;
    private const int OverlapMaxMinutes = 40; // 이 값 이하로 가까우면 겹침으로 본다
    private const int DeadlockMaxAttempts = 3;

    private static readonly TimeSpan EndTimeMin = new(14, 0, 0);
    private static readonly TimeSpan EndTimeMax = new(23, 30, 0);

    private readonly IScheduleRepository _scheduleRepository;
    private readonly ILogService _logService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly RideMateContext _context; // 트랜잭션 시작·완료에만 쓴다

    public ScheduleService(IScheduleRepository scheduleRepository,
        ILogService logService,
        IHttpContextAccessor httpContextAccessor,
        RideMateContext context)
    {
        this._scheduleRepository = scheduleRepository;
        this._logService = logService;
        this._httpContextAccessor = httpContextAccessor;
        this._context = context;
    }

    /// <summary>
    /// 시간표 등록
    /// </summary>
    public Task<ResponseModel<AddScheduleResultDto>> AddScheduleService(AddScheduleDto dto,
        CancellationToken cancellationToken = default)
    {
        return RetryOnDeadlockAsync(() => AddScheduleCoreAsync(dto, cancellationToken), cancellationToken);
    }

    // 교착은 RetryOnDeadlockAsync가 다시 실행하므로 여기서는 500으로 바꾸지 않고 올려보낸다
    private async Task<ResponseModel<AddScheduleResultDto>> AddScheduleCoreAsync(AddScheduleDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            // 1) 입력 검증 — API 모델 검증을 거치지 않은 호출도 같은 규칙으로 거절한다
            var input = ValidateInput(dto);
            if (input is null)
                return Fail(400, "잘못된 요청입니다.");

            // 2) 인증 확인 — sub 클레임이 없으면 거절하고, 계정이 있는지는 아래 잠금 조회가 확인한다
            var loginUserId = GetLoginUserId();
            if (loginUserId is null)
                return Fail(401, "잘못된 요청입니다.");

            DateTime thisTime = NowToSecond();
            var transaction = await _context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await using var transactionScope = transaction.ConfigureAwait(false);
            var dbTransaction = transaction.GetDbTransaction();

            // 3) 다른 조회보다 먼저 계정을 잠가 같은 계정의 동시 요청을 직렬화한다 — 잠근 뒤에 일반 조회를 해야 직전 커밋을 본다
            var lockedUserId = await _scheduleRepository.LockUserInfoAsync(loginUserId.Value, dbTransaction,
                cancellationToken).ConfigureAwait(false);
            if (lockedUserId is null) // 계정이 없거나 지워졌다
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return Fail(401, "잘못된 요청입니다.");
            }

            // 4) 살아 있는 아이가 없으면 아이 정보부터 등록하게 한다
            var childId = await _scheduleRepository.GetLiveChildIdAsync(lockedUserId.Value, dbTransaction,
                cancellationToken).ConfigureAwait(false);
            if (childId is null)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return Fail(101, "아이 정보를 먼저 등록해 주세요.");
            }

            // 5) 살아 있는 시간표가 10건 이상이면 거절한다 — 스위치 상태와 관계없이 센다
            var liveCount = await _scheduleRepository.CountLiveSchedulesAsync(childId.Value, dbTransaction,
                cancellationToken).ConfigureAwait(false);
            if (liveCount >= MaxScheduleCount)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return Fail(102, "시간표는 10개까지 등록할 수 있어요");
            }

            // 6) 좌표가 있으면 그 점을 담는 학원가를 찾는다. 없으면 학원가 밖이다
            int? areaId = null;
            if (input.Lat is not null && input.Lng is not null)
            {
                areaId = await _scheduleRepository.FindAreaIdAsync(input.Lat.Value, input.Lng.Value, dbTransaction,
                    cancellationToken).ConfigureAwait(false);
                if (areaId is null)
                {
                    await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    return Fail(104, "지금은 대치동 학원만 등록할 수 있어요");
                }
            }

            // 7) 같은 요일에 하원 시각이 40분 이하로 가까운 시간표가 있으면 경고하고 저장하지 않는다. 확인(confirmOverlap)한 뒤에는 그대로 저장한다
            if (!input.ConfirmOverlap)
            {
                var liveTimes = await _scheduleRepository.GetLiveScheduleTimesAsync(childId.Value, dbTransaction,
                    cancellationToken).ConfigureAwait(false);
                var overlaps = FindOverlaps(liveTimes, input);
                if (overlaps.Count > 0)
                {
                    await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    return new ResponseModel<AddScheduleResultDto>
                    {
                        message = "같은 요일에 시간이 가까운 시간표가 있어요.",
                        data = new AddScheduleResultDto { schedule = null, overlaps = overlaps },
                        code = 103
                    };
                }
            }

            // 8) 학원 — 정규화한 이름·주소가 같은 살아 있는 행을 쓰고, 없으면 새로 만든다
            var academy = await _scheduleRepository.FindAcademyAsync(input.AcademyName, input.Address, dbTransaction,
                cancellationToken).ConfigureAwait(false);
            var savedAcademyId = await SaveAcademyAsync(academy, input, areaId, thisTime, cancellationToken)
                .ConfigureAwait(false);
            if (savedAcademyId is null)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return Fail(500, "서버에서 요청을 처리하지 못하였습니다.");
            }

            int academyId = savedAcademyId.Value;

            // 9) 시간표 1행과 요일마다 1행(픽업 스위치 켠 채로) — 감사 컬럼은 Service가 채운다
            var timetable = new Timetable
            {
                ChildId = childId.Value,
                AcademyId = academyId,
                DisplayName = input.DisplayName,
                EndTime = input.EndTime,
                CreateDt = thisTime,
                CreateUser = AuditUser,
                UpdateDt = thisTime,
                UpdateUser = AuditUser,
                DeleteYn = false
            };
            var timetableDays = input.Days.Order().Select(day => new TimetableDay
            {
                DayOfWeek = (sbyte)day,
                PickupYn = true,
                CreateDt = thisTime,
                CreateUser = AuditUser,
                UpdateDt = thisTime,
                UpdateUser = AuditUser,
                DeleteYn = false
            }).ToList();
            if (!await _scheduleRepository.AddTimetableAsync(timetable, timetableDays, cancellationToken).ConfigureAwait(false))
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return Fail(500, "서버에서 요청을 처리하지 못하였습니다.");
            }

            // 10) 응답 — 방금 저장한 시간표를 목록 항목과 같은 모양으로 읽는다. 커밋 전에 읽어 읽기가 실패하면 저장도 취소된다
            var items = await _scheduleRepository.GetScheduleItemsAsync(childId.Value, timetable.TimetableId,
                dbTransaction, cancellationToken).ConfigureAwait(false);
            if (items.Count != 1)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return Fail(500, "서버에서 요청을 처리하지 못하였습니다.");
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            return new ResponseModel<AddScheduleResultDto>
            {
                message = "요청이 정상 처리되었습니다.",
                data = new AddScheduleResultDto { schedule = items[0], overlaps = null },
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
    /// 내 시간표 목록
    /// </summary>
    public async Task<ResponseModel<List<ScheduleItemDto>>> GetScheduleListService(
        CancellationToken cancellationToken = default)
    {
        try
        {
            // 1) 인증 확인 — sub 클레임이 없거나 계정이 없으면 거절한다
            var loginUserId = GetLoginUserId();
            if (loginUserId is null)
                return Fail<List<ScheduleItemDto>>(401, "잘못된 요청입니다.");

            var userId = await _scheduleRepository.GetLiveUserIdAsync(loginUserId.Value, cancellationToken)
                .ConfigureAwait(false);
            if (userId is null)
                return Fail<List<ScheduleItemDto>>(401, "잘못된 요청입니다.");

            // 2) 살아 있는 아이가 없으면 빈 목록이다
            var childId = await _scheduleRepository.GetLiveChildIdAsync(userId.Value,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            var items = childId is null
                ? new List<ScheduleItemDto>()
                : await _scheduleRepository.GetScheduleItemsAsync(childId.Value, null,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

            return new ResponseModel<List<ScheduleItemDto>>
            {
                message = "요청이 정상 처리되었습니다.",
                data = items,
                code = 200
            };
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested) // 요청 취소는 그대로 올려보낸다
        {
            LogServiceException(ex);
            return Fail<List<ScheduleItemDto>>(500, "서버에서 요청을 처리하지 못하였습니다.");
        }
    }

    /// <summary>
    /// 요일별 픽업 스위치 변경
    /// </summary>
    public Task<ResponseModel<ScheduleDayDto>> UpdateScheduleDayPickupService(UpdateScheduleDayPickupDto dto,
        CancellationToken cancellationToken = default)
    {
        return RetryOnDeadlockAsync(() => UpdateScheduleDayPickupCoreAsync(dto, cancellationToken), cancellationToken);
    }

    // 교착은 RetryOnDeadlockAsync가 다시 실행하므로 여기서는 500으로 바꾸지 않고 올려보낸다
    private async Task<ResponseModel<ScheduleDayDto>> UpdateScheduleDayPickupCoreAsync(
        UpdateScheduleDayPickupDto dto, CancellationToken cancellationToken)
    {
        try
        {
            // 1) 입력 검증 — API 모델 검증을 거치지 않은 호출도 같은 규칙으로 거절한다
            if (dto is not { scheduleDaySeq: >= 1, pickupYn: not null })
                return Fail<ScheduleDayDto>(400, "잘못된 요청입니다.");

            // 2) 인증 확인 — sub 클레임이 없으면 거절하고, 계정이 있는지는 아래 잠금 조회가 확인한다
            var loginUserId = GetLoginUserId();
            if (loginUserId is null)
                return Fail<ScheduleDayDto>(401, "잘못된 요청입니다.");

            DateTime thisTime = NowToSecond();
            var transaction = await _context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await using var transactionScope = transaction.ConfigureAwait(false);
            var dbTransaction = transaction.GetDbTransaction();

            // 3) 다른 조회보다 먼저 계정을 잠가 같은 계정의 동시 요청을 직렬화한다
            var lockedUserId = await _scheduleRepository.LockUserInfoAsync(loginUserId.Value, dbTransaction,
                cancellationToken).ConfigureAwait(false);
            if (lockedUserId is null) // 계정이 없거나 지워졌다
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return Fail<ScheduleDayDto>(401, "잘못된 요청입니다.");
            }

            // 4) 그 요일이 이 계정 아이의 살아 있는 시간표의 살아 있는 요일인지 확인한다. 아이가 없거나 남의 요일이면 찾을 수 없다
            var childId = await _scheduleRepository.GetLiveChildIdAsync(lockedUserId.Value, dbTransaction,
                cancellationToken).ConfigureAwait(false);
            var day = childId is null
                ? null
                : await _scheduleRepository.GetLiveScheduleDayAsync(childId.Value, dto.scheduleDaySeq.Value,
                    dbTransaction, cancellationToken).ConfigureAwait(false);
            if (day is null)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return Fail<ScheduleDayDto>(404, "대상을 찾을 수 없습니다.");
            }

            // 5) 픽업 스위치만 바꾼다 — 감사 컬럼은 Service가 채운다
            if (!await _scheduleRepository.UpdateScheduleDayPickupAsync(day.scheduleDaySeq, dto.pickupYn.Value,
                    thisTime, AuditUser, cancellationToken).ConfigureAwait(false))
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return Fail<ScheduleDayDto>(500, "서버에서 요청을 처리하지 못하였습니다.");
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            day.pickupYn = dto.pickupYn.Value;
            return new ResponseModel<ScheduleDayDto>
            {
                message = "요청이 정상 처리되었습니다.",
                data = day,
                code = 200
            };
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested && !IsDeadlock(ex)) // 요청 취소·교착은 그대로 올려보낸다
        {
            LogServiceException(ex);
            return Fail<ScheduleDayDto>(500, "서버에서 요청을 처리하지 못하였습니다.");
        }
    }

    /// <summary>
    /// 시간표 삭제
    /// </summary>
    public Task<ResponseModel<DeleteScheduleResultDto>> DeleteScheduleService(DeleteScheduleDto dto,
        CancellationToken cancellationToken = default)
    {
        return RetryOnDeadlockAsync(() => DeleteScheduleCoreAsync(dto, cancellationToken), cancellationToken);
    }

    // 교착은 RetryOnDeadlockAsync가 다시 실행하므로 여기서는 500으로 바꾸지 않고 올려보낸다
    private async Task<ResponseModel<DeleteScheduleResultDto>> DeleteScheduleCoreAsync(DeleteScheduleDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            // 1) 입력 검증 — API 모델 검증을 거치지 않은 호출도 같은 규칙으로 거절한다
            if (dto is not { scheduleSeq: >= 1 })
                return Fail<DeleteScheduleResultDto>(400, "잘못된 요청입니다.");

            // 2) 인증 확인 — sub 클레임이 없으면 거절하고, 계정이 있는지는 아래 잠금 조회가 확인한다
            var loginUserId = GetLoginUserId();
            if (loginUserId is null)
                return Fail<DeleteScheduleResultDto>(401, "잘못된 요청입니다.");

            // 3) 잠글 학원가를 트랜잭션 밖에서 고른다 — 트랜잭션 안에서 먼저 조회하면 AREA 잠금 전의 스냅샷이 고정된다(schema.md 배정 1단계).
            //    없으면(남의 시간표·좌표 없는 학원) 잠그지 않고, 소유는 아래 잠금 뒤 조회가 다시 확인한다
            var areaId = await _scheduleRepository.GetScheduleAreaIdAsync(loginUserId.Value, dto.scheduleSeq.Value,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            DateTime thisTime = NowToSecond();
            var transaction = await _context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await using var transactionScope = transaction.ConfigureAwait(false);
            var dbTransaction = transaction.GetDbTransaction();

            // 4) 첫 문장으로 학원가를 잠근다 — 계획 생성(AREA → TIMETABLE_DAY)과 같은 잠금을 먼저 잡아 삭제 도중에 새 계획이 끼어들지 못한다
            if (areaId is not null)
            {
                await _scheduleRepository.LockAreaAsync(areaId.Value, dbTransaction, cancellationToken)
                    .ConfigureAwait(false);
            }

            // 5) 계정을 잠가 같은 계정의 시간표 등록·스위치 변경과 직렬화한다 — 잠근 뒤에 일반 조회를 해야 직전 커밋을 본다
            var lockedUserId = await _scheduleRepository.LockUserInfoAsync(loginUserId.Value, dbTransaction,
                cancellationToken).ConfigureAwait(false);
            if (lockedUserId is null) // 계정이 없거나 지워졌다
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return Fail<DeleteScheduleResultDto>(401, "잘못된 요청입니다.");
            }

            // 6) 일반 조회 전에 잠금 읽기로 최신 학원가를 다시 보고, 잠그지 않은 학원가면 지금 잠근다 — 트랜잭션 밖 조회 뒤에
            //    다른 계정이 좌표를 채웠거나 같은 계정의 편집이 학원을 다른 학원가로 옮겼을 수 있다
            await LockLatestScheduleAreaAsync(lockedUserId.Value, dto.scheduleSeq.Value,
                areaId is null ? [] : [areaId.Value], dbTransaction, cancellationToken).ConfigureAwait(false);

            // 7) 이 계정 아이의 살아 있는 시간표인지 확인한다. 아이가 없거나 남의 것이거나 이미 지웠으면 찾을 수 없다
            var childId = await _scheduleRepository.GetLiveChildIdAsync(lockedUserId.Value, dbTransaction,
                cancellationToken).ConfigureAwait(false);
            var dayIds = childId is null
                ? null
                : await _scheduleRepository.GetLiveScheduleDayIdsAsync(childId.Value, dto.scheduleSeq.Value,
                    dbTransaction, cancellationToken).ConfigureAwait(false);
            if (dayIds is null)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return Fail<DeleteScheduleResultDto>(404, "대상을 찾을 수 없습니다.");
            }

            // 8) 진행 중인 픽업 계획이 있으면 지우지 않는다. 학원가 잠금으로 새 계획은 생길 수 없으니 일반 조회로 후보를 찾고,
            //    후보만 PK 순서로 잠가 최신 상태를 다시 본다 — 그사이 끝난 계획은 빠진다. 계획을 끝내는 트랜잭션과 같이 계획 PK부터 잠근다
            if (dayIds.Count > 0)
            {
                var candidatePlanIds = await _scheduleRepository.GetActivePickupPlanIdsAsync(dayIds, dbTransaction,
                    cancellationToken).ConfigureAwait(false);
                var activePlanIds = candidatePlanIds.Count == 0
                    ? candidatePlanIds
                    : await _scheduleRepository.LockActivePickupPlanIdsAsync(candidatePlanIds, dayIds, dbTransaction,
                        cancellationToken).ConfigureAwait(false);
                if (activePlanIds.Count > 0)
                {
                    await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    return Fail<DeleteScheduleResultDto>(409, "잘못된 요청입니다.");
                }
            }

            // 9) 날짜별 예외 → 요일 → 시간표 순으로 지운다 — 감사 컬럼은 Service가 채운다
            if (!await _scheduleRepository.DeleteScheduleAsync(dto.scheduleSeq.Value, dayIds, thisTime, AuditUser,
                    cancellationToken).ConfigureAwait(false))
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return Fail<DeleteScheduleResultDto>(500, "서버에서 요청을 처리하지 못하였습니다.");
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            return new ResponseModel<DeleteScheduleResultDto>
            {
                message = "요청이 정상 처리되었습니다.",
                data = new DeleteScheduleResultDto { scheduleSeq = dto.scheduleSeq.Value },
                code = 200
            };
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested && !IsDeadlock(ex)) // 요청 취소·교착은 그대로 올려보낸다
        {
            LogServiceException(ex);
            return Fail<DeleteScheduleResultDto>(500, "서버에서 요청을 처리하지 못하였습니다.");
        }
    }

    /// <summary>
    /// 시간표 편집
    /// </summary>
    public Task<ResponseModel<AddScheduleResultDto>> UpdateScheduleService(UpdateScheduleDto dto,
        CancellationToken cancellationToken = default)
    {
        return RetryOnDeadlockAsync(() => UpdateScheduleCoreAsync(dto, cancellationToken), cancellationToken);
    }

    // 교착은 RetryOnDeadlockAsync가 다시 실행하므로 여기서는 500으로 바꾸지 않고 올려보낸다
    private async Task<ResponseModel<AddScheduleResultDto>> UpdateScheduleCoreAsync(UpdateScheduleDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            // 1) 입력 검증 — 등록과 같은 규칙으로 보고 시간표 ID를 더 본다
            var input = ValidateInput(dto);
            if (input is null || dto.scheduleSeq is not >= 1)
                return Fail(400, "잘못된 요청입니다.");
            var scheduleSeq = dto.scheduleSeq.Value;

            // 2) 인증 확인 — sub 클레임이 없으면 거절하고, 계정이 있는지는 아래 잠금 조회가 확인한다
            var loginUserId = GetLoginUserId();
            if (loginUserId is null)
                return Fail(401, "잘못된 요청입니다.");

            // 3) 잠글 학원가를 트랜잭션 밖에서 고른다 — 지금 학원의 학원가(삭제와 같다)와 새 좌표의 학원가.
            //    새 학원 행을 넣거나 좌표를 채우면 FK로 새 학원가에 공유 잠금이 걸리므로 그 학원가도 미리 잠근다(schema.md TIMETABLE 절)
            var currentAreaId = await _scheduleRepository.GetScheduleAreaIdAsync(loginUserId.Value, scheduleSeq,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            int? inputAreaId = null;
            if (input.Lat is not null && input.Lng is not null)
            {
                inputAreaId = await _scheduleRepository.FindAreaIdAsync(input.Lat.Value, input.Lng.Value,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
            }

            DateTime thisTime = NowToSecond();
            var transaction = await _context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await using var transactionScope = transaction.ConfigureAwait(false);
            var dbTransaction = transaction.GetDbTransaction();

            // 4) 첫 문장으로 학원가들을 AREA_ID 순서로 잠근다 — 계획 생성(AREA → TIMETABLE_DAY)과 같은 잠금을 먼저 잡아 편집 도중에 새 계획이 끼어들지 못한다
            var lockedAreaIds = new[] { currentAreaId, inputAreaId }
                .Where(x => x is not null)
                .Select(x => x!.Value)
                .Distinct()
                .Order()
                .ToList();
            foreach (var lockAreaId in lockedAreaIds)
            {
                await _scheduleRepository.LockAreaAsync(lockAreaId, dbTransaction, cancellationToken)
                    .ConfigureAwait(false);
            }

            // 5) 계정을 잠가 같은 계정의 시간표 등록·스위치 변경·삭제와 직렬화한다 — 잠근 뒤에 일반 조회를 해야 직전 커밋을 본다
            var lockedUserId = await _scheduleRepository.LockUserInfoAsync(loginUserId.Value, dbTransaction,
                cancellationToken).ConfigureAwait(false);
            if (lockedUserId is null) // 계정이 없거나 지워졌다
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return Fail(401, "잘못된 요청입니다.");
            }

            // 6) 삭제와 같이 잠금 읽기로 지금 학원의 최신 학원가를 다시 보고, 잠그지 않은 학원가면 지금 잠근다
            await LockLatestScheduleAreaAsync(lockedUserId.Value, scheduleSeq, lockedAreaIds, dbTransaction,
                cancellationToken).ConfigureAwait(false);

            // 7) 이 계정 아이의 살아 있는 시간표인지 확인한다. 아이가 없거나 남의 것이거나 지웠으면 찾을 수 없다
            var childId = await _scheduleRepository.GetLiveChildIdAsync(lockedUserId.Value, dbTransaction,
                cancellationToken).ConfigureAwait(false);
            var schedule = childId is null
                ? null
                : await _scheduleRepository.GetLiveScheduleAsync(childId.Value, scheduleSeq, dbTransaction,
                    cancellationToken).ConfigureAwait(false);
            if (childId is null || schedule is null)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return Fail(404, "대상을 찾을 수 없습니다.");
            }

            // 8) 좌표가 있는데 학원가 밖이면 등록과 같이 거절한다
            if (input.Lat is not null && inputAreaId is null)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return Fail(104, "지금은 대치동 학원만 등록할 수 있어요");
            }

            // 9) 바뀌는 것을 고른다 — 학원은 정규화한 이름·주소로 찾은 행이 지금 학원과 다르면 바뀐 것이다.
            //    뺀 요일은 지우고, 다시 넣은 요일은 삭제 표시된 행을 되살리고, 처음 넣는 요일만 새 행을 만든다
            var academy = await _scheduleRepository.FindAcademyAsync(input.AcademyName, input.Address, dbTransaction,
                cancellationToken).ConfigureAwait(false);
            var academyChanged = academy is null || academy.AcademyId != schedule.AcademyId;
            var endTimeChanged = input.EndTime != schedule.EndTime;

            var dayRows = await _scheduleRepository.GetScheduleDayRowsAsync(scheduleSeq, dbTransaction,
                cancellationToken).ConfigureAwait(false);
            var liveDays = dayRows.Where(x => !x.DeleteYn).ToList();
            var deleteDayIds = liveDays
                .Where(x => !input.Days.Contains(x.DayOfWeek))
                .Select(x => x.TimetableDayId)
                .ToList();
            var reviveDayIds = new List<int>();
            var newDays = new List<TimetableDay>();
            foreach (var day in input.Days.Order().Where(d => liveDays.All(x => x.DayOfWeek != d)))
            {
                var deletedRow = dayRows.FirstOrDefault(x => x.DeleteYn && x.DayOfWeek == day);
                if (deletedRow is not null)
                {
                    reviveDayIds.Add(deletedRow.TimetableDayId);
                    continue;
                }

                newDays.Add(new TimetableDay
                {
                    TimetableId = scheduleSeq,
                    DayOfWeek = (sbyte)day,
                    PickupYn = true,
                    CreateDt = thisTime,
                    CreateUser = AuditUser,
                    UpdateDt = thisTime,
                    UpdateUser = AuditUser,
                    DeleteYn = false
                });
            }

            // 10) 진행 중인 픽업 계획이 있으면 학원·하원 시각 변경과 그 계획의 요일 빼기를 거절한다. 삭제와 같이 일반 조회로 후보를 찾고
            //     후보만 PK 순서로 잠가 최신 상태를 다시 본다. 이름 변경과 계획이 없는 요일의 추가·빼기는 허용한다
            var checkDayIds = academyChanged || endTimeChanged
                ? dayRows.Select(x => x.TimetableDayId).ToList()
                : deleteDayIds;
            if (checkDayIds.Count > 0)
            {
                var candidatePlanIds = await _scheduleRepository.GetActivePickupPlanIdsAsync(checkDayIds, dbTransaction,
                    cancellationToken).ConfigureAwait(false);
                var activePlanIds = candidatePlanIds.Count == 0
                    ? candidatePlanIds
                    : await _scheduleRepository.LockActivePickupPlanIdsAsync(candidatePlanIds, checkDayIds,
                        dbTransaction, cancellationToken).ConfigureAwait(false);
                if (activePlanIds.Count > 0)
                {
                    await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    return Fail(409, "잘못된 요청입니다.");
                }
            }

            // 11) 같은 요일에 하원 시각이 40분 이하로 가까운 다른 시간표가 있으면 등록과 같이 경고하고 저장하지 않는다. 자기 자신은 뺀다
            if (!input.ConfirmOverlap)
            {
                var liveTimes = await _scheduleRepository.GetLiveScheduleTimesAsync(childId.Value, dbTransaction,
                    cancellationToken).ConfigureAwait(false);
                var overlaps = FindOverlaps(liveTimes.Where(x => x.TimetableId != scheduleSeq), input);
                if (overlaps.Count > 0)
                {
                    await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    return new ResponseModel<AddScheduleResultDto>
                    {
                        message = "같은 요일에 시간이 가까운 시간표가 있어요.",
                        data = new AddScheduleResultDto { schedule = null, overlaps = overlaps },
                        code = 103
                    };
                }
            }

            // 12) 학원 — 등록과 같이 찾은 행을 쓰고, 없으면 새로 만들고, 좌표가 없던 행이면 채운다
            var academyId = await SaveAcademyAsync(academy, input, inputAreaId, thisTime, cancellationToken)
                .ConfigureAwait(false);
            if (academyId is null)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return Fail(500, "서버에서 요청을 처리하지 못하였습니다.");
            }

            // 13) 요일과 시간표를 바꾼다 — 감사 컬럼은 Service가 채운다
            if (!await _scheduleRepository.UpdateScheduleAsync(scheduleSeq, academyId.Value, input.DisplayName,
                    input.EndTime, deleteDayIds, reviveDayIds, newDays, thisTime, AuditUser, cancellationToken)
                    .ConfigureAwait(false))
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return Fail(500, "서버에서 요청을 처리하지 못하였습니다.");
            }

            // 14) 응답 — 등록과 같이 커밋 전에 목록 항목 모양으로 읽는다
            var items = await _scheduleRepository.GetScheduleItemsAsync(childId.Value, scheduleSeq, dbTransaction,
                cancellationToken).ConfigureAwait(false);
            if (items.Count != 1)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return Fail(500, "서버에서 요청을 처리하지 못하였습니다.");
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            return new ResponseModel<AddScheduleResultDto>
            {
                message = "요청이 정상 처리되었습니다.",
                data = new AddScheduleResultDto { schedule = items[0], overlaps = null },
                code = 200
            };
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested && !IsDeadlock(ex)) // 요청 취소·교착은 그대로 올려보낸다
        {
            LogServiceException(ex);
            return Fail(500, "서버에서 요청을 처리하지 못하였습니다.");
        }
    }

    // 시간표 삭제·편집 6단계 — USER_INFO를 잠근 뒤 일반 조회 전에 부른다. 잠금 읽기(FOR SHARE)로 시간표 학원의 최신 학원가를 읽고
    // 학원 행을 공유 잠금해 그사이 다른 계정이 좌표·학원가를 채우지 못하게 한다. 학원을 바꾸는 편집은 USER_INFO를 잡아야 하므로 이 값은 커밋까지 바뀌지 않는다.
    // 이미 잠근 학원가가 아니면 지금 잠근다 — 순서는 어긋나지만 일반 조회 전이라 아래 조회가 그 잠금 뒤의 커밋을 본다(드문 교착은 재시도)
    private async Task LockLatestScheduleAreaAsync(int userId, int scheduleSeq, IReadOnlyCollection<int> lockedAreaIds,
        IDbTransaction dbTransaction, CancellationToken cancellationToken)
    {
        var latestAreaId = await _scheduleRepository.GetScheduleAreaIdAsync(userId, scheduleSeq, dbTransaction,
            cancellationToken).ConfigureAwait(false);
        if (latestAreaId is not null && !lockedAreaIds.Contains(latestAreaId.Value))
        {
            await _scheduleRepository.LockAreaAsync(latestAreaId.Value, dbTransaction, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    // 로그인한 계정(JWT sub)의 ID — 클레임이 없거나 숫자가 아니면 null. 부른 쪽이 401로 바꾼다
    private int? GetLoginUserId()
    {
        return int.TryParse(_httpContextAccessor.HttpContext?.User.FindFirstValue("sub"), out var userId)
            ? userId
            : null;
    }

    // 검증을 통과한 값(앞뒤 공백을 지우고 학원 이름·주소는 연속 공백을 한 칸으로 줄인 값)
    private sealed record ValidInput(string DisplayName, string AcademyName, string Address, decimal? Lat, decimal? Lng,
        List<int> Days, TimeSpan EndTime, bool ConfirmOverlap);

    // 스펙 표의 규칙대로 검증한다. 하나라도 어긋나면 null이다
    private static ValidInput? ValidateInput(AddScheduleDto? dto)
    {
        if (dto is null)
            return null;

        // 학원 이름(부모가 지은 이름): 앞뒤 공백을 지운 뒤 1~20자(코드 포인트), 한글·영문·숫자·공백과 ( ) - · 만
        var displayName = dto.displayName?.Trim();
        if (displayName is null || !IsLengthBetween(displayName, 1, DisplayNameMaxLength)
            || !Regex.IsMatch(displayName, @"\A[가-힣A-Za-z0-9 ()\-·]+\z"))
            return null;

        // 장소: 이름 1~100자, 주소 1~200자(앞뒤 공백을 지운 뒤)
        var place = dto.place;
        var academyName = place?.name?.Trim();
        var address = place?.address?.Trim();
        if (place is null || academyName is null || address is null
            || !IsLengthBetween(academyName, 1, AcademyNameMaxLength)
            || !IsLengthBetween(address, 1, AddressMaxLength))
            return null;

        // 좌표: 둘 다 있거나 둘 다 없어야 하고, 위도 -90~90, 경도 -180~180
        if ((place.lat is null) != (place.lng is null))
            return null;
        if (place.lat is not null && (place.lat < -90m || place.lat > 90m || place.lng < -180m || place.lng > 180m))
            return null;

        // 요일: 1~7개, 각 1~7, 중복 없음
        var days = dto.days;
        if (days is null || days.Count < 1 || days.Count > 7 || days.Any(x => x < 1 || x > 7)
            || days.Distinct().Count() != days.Count)
            return null;

        // 하원 시각: HH:mm, 14:00~23:30, 5분 단위
        if (dto.endTime is null || !Regex.IsMatch(dto.endTime, @"\A([01][0-9]|2[0-3]):[0-5][0-9]\z"))
            return null;
        var hour = int.Parse(dto.endTime[..2], CultureInfo.InvariantCulture);
        var minute = int.Parse(dto.endTime[3..], CultureInfo.InvariantCulture);
        var endTime = new TimeSpan(hour, minute, 0);
        if (endTime < EndTimeMin || endTime > EndTimeMax || minute % 5 != 0)
            return null;

        return new ValidInput(displayName, NormalizeText(academyName), NormalizeText(address), place.lat, place.lng,
            days, endTime, dto.confirmOverlap == true);
    }

    // 앞뒤 공백을 지운 값이 min~max자(코드 포인트)인지 본다
    private static bool IsLengthBetween(string trimmed, int min, int max)
    {
        var length = trimmed.EnumerateRunes().Count();
        return length >= min && length <= max;
    }

    // 학원 이름·주소 비교와 저장에 쓰는 정규화: 앞뒤 공백 제거 + 연속 공백 한 칸
    private static string NormalizeText(string value)
    {
        return Regex.Replace(value.Trim(), @"\s+", " ");
    }

    private static string FormatTime(TimeSpan time)
    {
        return $"{(int)time.TotalHours:00}:{time.Minutes:00}";
    }

    // 같은 요일에 하원 시각이 40분 이하로 가까운 시간표를 (시간표, 요일) 한 건씩 겹침 목록으로 바꾼다
    private static List<ScheduleOverlapItemDto> FindOverlaps(IEnumerable<LiveScheduleTimeRow> liveTimes, ValidInput input)
    {
        return liveTimes
            .Where(x => input.Days.Contains(x.DayOfWeek)
                        && Math.Abs((x.EndTime - input.EndTime).TotalMinutes) <= OverlapMaxMinutes)
            .Select(x => new ScheduleOverlapItemDto
            {
                scheduleSeq = x.TimetableId,
                displayName = x.DisplayName,
                dayOfWeek = x.DayOfWeek,
                endTime = FormatTime(x.EndTime)
            })
            .ToList();
    }

    // 학원 — 정규화한 이름·주소로 찾은 행(academy)을 쓰고, 없으면 새로 만든다. 찾은 행에 좌표가 없고 이번 요청에 좌표가 있으면 학원가·좌표를 채운다.
    // 쓸 학원 ID를 돌려주고, 새 행 저장에 실패하면 null이다
    private async Task<int?> SaveAcademyAsync(Academy? academy, ValidInput input, int? areaId, DateTime thisTime,
        CancellationToken cancellationToken)
    {
        if (academy is null)
        {
            var newAcademy = new Academy
            {
                AreaId = areaId,
                AcademyName = input.AcademyName,
                Address = input.Address,
                Lat = input.Lat,
                Lng = input.Lng,
                SourceType = AcademySourceTypePlace,
                PlaceId = null,
                CreateDt = thisTime,
                CreateUser = AuditUser,
                UpdateDt = thisTime,
                UpdateUser = AuditUser,
                DeleteYn = false
            };
            if (!await _scheduleRepository.AddAcademyAsync(newAcademy, cancellationToken).ConfigureAwait(false))
                return null;

            return newAcademy.AcademyId;
        }

        // 기존 행에 좌표가 없고 이번 요청에 좌표가 있으면 학원가·좌표를 채운다
        if (academy.Lat is null && areaId is not null)
        {
            await _scheduleRepository.UpdateAcademyLocationAsync(academy.AcademyId, areaId.Value, input.Lat!.Value,
                input.Lng!.Value, thisTime, AuditUser, cancellationToken).ConfigureAwait(false);
        }

        return academy.AcademyId;
    }

    private static ResponseModel<AddScheduleResultDto> Fail(int code, string message)
    {
        return Fail<AddScheduleResultDto>(code, message);
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
