using System.Data;
using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Storage;
using MySql.Data.MySqlClient;
using RideMateService.Comm;
using RideMateService.Comm.Dto.v1.Pickup.Request;
using RideMateService.Comm.Dto.v1.Pickup.Response;
using RideMateService.Commons.DBModels;
using RideMateService.Repository.v1.Pickup;

namespace RideMateService.Service.v1.Pickup;

public partial class PickupService
{
    private const string PlanStatusPlanned = "PLANNED";
    private const string EventPlanned = "PLANNED";
    private const string LocationTypeSpot = "SPOT";
    private const string LocationTypeCustom = "CUSTOM";
    private const string ReasonTooEarly = "TOO_EARLY";
    private const int PlanAreaRetryMaxAttempts = 3;

    /// <summary>
    /// 오늘 픽업 계획 만들기(이미 있으면 그 계획). 기본 지점을 우선순위·금지 시간·정원으로 배정한다
    /// </summary>
    public async Task<ResponseModel<PickupPlanDto>> AddPickupPlanService(AddPickupPlanDto dto,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // 1) 인증 확인 — 로그인 사용자(USER_ID)는 HttpContext의 클레임 sub에서 꺼낸다
            if (!int.TryParse(_httpContextAccessor.HttpContext?.User.FindFirstValue("sub"), out var userId))
                return new ResponseModel<PickupPlanDto> { message = "잘못된 요청입니다.", data = null, code = 401 };

            // 2) 입력 검증 — 픽업 좌표는 둘 다 있거나 둘 다 없어야 한다
            if (dto is null || (dto.pickupLat is null) != (dto.pickupLng is null))
                return new ResponseModel<PickupPlanDto> { message = "잘못된 요청입니다.", data = null, code = 400 };

            // 임의 좌표 계획은 아직 만들지 않는다(설계 6.1 처리 5, 다음 작업에서 채운다)
            if (dto.pickupLat is not null)
                return new ResponseModel<PickupPlanDto> { message = "잘못된 요청입니다.", data = null, code = 400 };

            // 3) 잠근 시간표의 학원가가 잠근 학원가와 다르면(그사이 시간표 편집) 대상 고르기부터 다시 한다
            for (var attempt = 1; attempt <= PlanAreaRetryMaxAttempts; attempt++)
            {
                var result = await TryAddPickupPlanAsync(userId, cancellationToken).ConfigureAwait(false);
                if (result is not null)
                    return result;
            }

            _logService.LogMessage($"pickup plan create gave up: area changed {PlanAreaRetryMaxAttempts} times, user={userId}");
            return new ResponseModel<PickupPlanDto> { message = "서버에서 요청을 처리하지 못하였습니다.", data = null, code = 500 };
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested) // 요청 취소는 그대로 올려보낸다
        {
            _logService.LogMessage(ex.ToString());
#if DEBUG
            _logService.ConsoleWarning(ex.ToString());
#endif
            return new ResponseModel<PickupPlanDto> { message = "서버에서 요청을 처리하지 못하였습니다.", data = null, code = 500 };
        }
    }

    // 계획 만들기 한 번(설계 6.1 처리 1~7). 잠근 학원가가 틀려 처음부터 다시 해야 하면 null
    private async Task<ResponseModel<PickupPlanDto>?> TryAddPickupPlanAsync(int userId, CancellationToken cancellationToken)
    {
        var now = DateTime.Now;
        var date = DateOnly.FromDateTime(now);
        var dayStart = date.ToDateTime(TimeOnly.MinValue);
        var leadTime = TimeSpan.FromHours(_planOptions.CreateLeadHours);

        // 1) 대상 픽업 — 진행 중 계획이 있으면 그것을 돌려준다(잠금 없이 읽는다)
        var target = await SelectPickupTargetAsync(userId, now, now, cancellationToken).ConfigureAwait(false);

        // 2) 진행 중 계획도 오늘 남은 픽업도 없다
        if (target is null)
            return new ResponseModel<PickupPlanDto> { message = "요청이 정상 처리되었습니다.", data = null, code = 101 };

        if (target.Plan is { } livePlan)
            return PlanOk(await BuildPlanDtoAsync(livePlan.PickupPlanId, false, null, cancellationToken).ConfigureAwait(false));

        var pickup = target.Pickup!;

        // 3) 시간 창 — 하원 시각 − CreateLeadHours보다 이르면 아직 만들 수 없다
        if (now < dayStart + pickup.EndTime - leadTime)
            return PlanConflict(ReasonTooEarly);

        // 4) 학원 좌표나 학원가가 없으면 지점을 배정할 수 없어 계획을 만들지 않는다
        if (pickup.AcademyLat is null || pickup.AcademyLng is null || pickup.AreaId is not { } areaId)
            return PlanConflict(ReasonNoAcademyLocation);

        // 요일 유형 — 그해 공휴일이 적재되지 않았으면 공휴일이 아닌 것으로 본다. 기준 데이터라 잠그기 전에 읽는다
        var holiday = await _pickupRepository.GetHolidayAsync(date, cancellationToken).ConfigureAwait(false);
        var dayType = DepartureGuideCalculator.DayType(date, holiday.IsHoliday);

        // 6) 트랜잭션 — 예외로 빠져나가면 await using이 트랜잭션을 롤백한다
        var transaction = await _context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using var transactionScope = transaction.ConfigureAwait(false);
        var dbTransaction = transaction.GetDbTransaction();

        // 6-1) 학원가 행 잠금이 첫 문장이다. 이보다 먼저 조회하면 스냅샷이 고정돼 직전에 커밋된 계획을 못 본다
        if (!await _pickupRepository.LockAreaAsync(areaId, dbTransaction, cancellationToken).ConfigureAwait(false))
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return null;
        }

        // 6-2) 시간표 요일 행을 잠그고 다시 확인한다(삭제·스위치·마친 픽업·그날 SKIP)
        var day = await _pickupRepository.LockTimetableDayAsync(pickup.TimetableDayId, date, dbTransaction,
            cancellationToken).ConfigureAwait(false);
        // 기준 시각은 잠금을 얻은 뒤에 잡는다. 잠금을 기다린 사이 만료된 계획이 정원을 잡지 않게 한다
        var thisTime = DateTime.Now;
        if (day is null || day.UserId != userId || !day.Alive || !day.PickupYn || day.Skipped
            || day.LastPickupDate == date)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return new ResponseModel<PickupPlanDto> { message = "요청이 정상 처리되었습니다.", data = null, code = 101 };
        }

        // 잠금을 기다리는 사이 시간표 편집이 학원을 다른 학원가로 옮겼으면 잠근 학원가가 틀리다
        if (day.AreaId != areaId)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return null;
        }

        // 잠근 값으로 학원 좌표와 시간 창을 다시 본다(그사이 학원이나 그날 하원 시각이 바뀌었을 수 있다)
        if (day.AcademyLat is null || day.AcademyLng is null)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return PlanConflict(ReasonNoAcademyLocation);
        }

        if (thisTime < dayStart + day.EndTime - leadTime)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return PlanConflict(ReasonTooEarly);
        }

        // 6-3) 그사이 다른 요청이 같은 요일의 계획을 만들었으면 그 계획을 돌려준다
        var existingPlanId = await _pickupRepository.GetLivePlanIdByTimetableDayAsync(day.TimetableDayId, date,
            dbTransaction, cancellationToken).ConfigureAwait(false);
        if (existingPlanId is { } existingId)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return PlanOk(await BuildPlanDtoAsync(existingId, false, null, cancellationToken).ConfigureAwait(false));
        }

        // 6-4) 기본 지점 배정. 쓸 지점이 없으면 지점·시각·도보 시간을 비운 채 만든다
        var assignment = await AssignDefaultSpotAsync(day.AcademyId, date, day.EndTime, dayType, thisTime, null, null,
            dbTransaction, cancellationToken).ConfigureAwait(false);

        // 6-5) 계획과 PLANNED 이벤트를 넣는다. 감사 컬럼은 Service가 채운다
        var plan = new PickupPlan
        {
            UserId = userId,
            ChildId = day.ChildId,
            TimetableDayId = day.TimetableDayId,
            AcademyId = day.AcademyId,
            AreaId = areaId,
            PickupDate = dayStart,
            EndTime = day.EndTime,
            PickupSpotId = assignment?.PickupSpotId,
            SlotDt = assignment?.SlotDt,
            WalkMin = assignment is null ? null : (sbyte)assignment.WalkMin,
            CustomLat = null,
            CustomLng = null,
            CustomName = null,
            ExpireDt = (assignment?.SlotDt ?? dayStart + day.EndTime).AddHours(_planOptions.ExpireAfterHours),
            Status = PlanStatusPlanned,
            CreateDt = thisTime,
            CreateUser = AuditUser,
            UpdateDt = thisTime,
            UpdateUser = AuditUser,
            DeleteYn = false
        };

        try
        {
            if (!await _pickupRepository.AddPickupPlanAsync(plan, cancellationToken).ConfigureAwait(false))
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return new ResponseModel<PickupPlanDto> { message = "서버에서 요청을 처리하지 못하였습니다.", data = null, code = 500 };
            }
        }
        catch (Exception ex) when (IsPlanDuplicateKey(ex)) // 7) UK (TIMETABLE_DAY_ID, PICKUP_DATE) — 이미 만들어진 계획을 돌려준다
        {
            _context.ChangeTracker.Clear();
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            var duplicatedId = await _pickupRepository.GetLivePlanIdByTimetableDayAsync(day.TimetableDayId, date, null,
                cancellationToken).ConfigureAwait(false);
            if (duplicatedId is null)
            {
                _logService.LogMessage($"pickup plan duplicate key but no plan: timetableDay={day.TimetableDayId}");
                return new ResponseModel<PickupPlanDto> { message = "서버에서 요청을 처리하지 못하였습니다.", data = null, code = 500 };
            }

            return PlanOk(await BuildPlanDtoAsync(duplicatedId.Value, false, null, cancellationToken).ConfigureAwait(false));
        }

        var plannedEvent = new PickupEvent
        {
            PickupPlanId = plan.PickupPlanId,
            EventType = EventPlanned,
            EventDt = thisTime,
            ClientEventId = null,
            Detail = JsonSerializer.Serialize(new PlannedDetail(plan.PickupSpotId, false, FormatEventDt(plan.SlotDt))),
            CreateDt = thisTime,
            CreateUser = AuditUser,
            UpdateDt = thisTime,
            UpdateUser = AuditUser,
            DeleteYn = false
        };
        if (!await _pickupRepository.AddPickupEventAsync(plannedEvent, cancellationToken).ConfigureAwait(false))
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return new ResponseModel<PickupPlanDto> { message = "서버에서 요청을 처리하지 못하였습니다.", data = null, code = 500 };
        }

        await transaction.CommitAsync(CancellationToken.None).ConfigureAwait(false);

        return PlanOk(await BuildPlanDtoAsync(plan.PickupPlanId, false, null, cancellationToken).ConfigureAwait(false));
    }

    // 대상 픽업 고르기(설계 3절). 출발 안내와 계획 API가 같은 규칙을 쓴다.
    // 1) 진행 중 계획(서버 시각 now 기준)이 있으면 그것. 여럿이면 배정 시각(없으면 하원 시각)이 가장 이른 것
    // 2) 없으면 windowNow 날짜의 오늘 픽업 중 하원 + PickupWindowHours가 windowNow보다 늦은 것에서 하원이 가장 이른 것
    private async Task<PickupTarget?> SelectPickupTargetAsync(int userId, DateTime now, DateTime windowNow,
        CancellationToken cancellationToken)
    {
        var livePlan = await _pickupRepository.GetLivePlanAsync(userId, DateOnly.FromDateTime(now), now,
            cancellationToken).ConfigureAwait(false);
        if (livePlan is not null)
            return new PickupTarget(livePlan, null);

        var date = DateOnly.FromDateTime(windowNow);
        var todays = await _pickupRepository.GetTodayPickupsAsync(userId, date,
            DepartureGuideCalculator.DayOfWeek(date), cancellationToken).ConfigureAwait(false);

        var dayStart = date.ToDateTime(TimeOnly.MinValue);
        var window = TimeSpan.FromHours(_options.PickupWindowHours);
        var pickup = todays
            .Where(t => dayStart + t.EndTime + window > windowNow)
            .OrderBy(t => t.EndTime)
            .ThenBy(t => t.TimetableDayId)
            .FirstOrDefault();

        return pickup is null ? null : new PickupTarget(null, pickup);
    }

    // 기본 지점 배정(설계 6.1 "기본 지점 배정 순서"). 후보를 PRIORITY 순으로 보며(USE_YN = 0은 조회에서 빠진다)
    // 아이 도착 시각이 금지 시간 [시작, 종료)에 걸리면 건너뛴다. 남은 지점마다 시작 시각 = max(아이 도착, notBefore)부터
    // SlotShiftMaxMin까지 1분씩 늦춰 정원이 남은 첫 시각을 쓴다. 늦춘 시각은 금지 시간을 다시 보지 않는다. 없으면 null
    private async Task<SlotAssignment?> AssignDefaultSpotAsync(int academyId, DateOnly date, TimeSpan endTime,
        string dayType, DateTime now, int? excludePlanId, DateTime? notBefore, IDbTransaction tx,
        CancellationToken cancellationToken)
    {
        var spots = await _pickupRepository.GetAcademyPickupSpotsAsync(academyId, cancellationToken).ConfigureAwait(false);
        if (spots.Count == 0)
            return null;

        var blocks = await _pickupRepository.GetPickupSpotBlocksAsync(spots.Select(s => s.PickupSpotId).ToArray(), dayType,
            cancellationToken).ConfigureAwait(false);

        foreach (var spot in spots)
        {
            var arrival = DepartureGuideCalculator.ChildArrival(date, endTime, _options.TidyUpMin, spot.WalkMin);
            var arrivalTime = arrival.TimeOfDay;
            if (blocks.Any(b => b.PickupSpotId == spot.PickupSpotId
                                && b.StartTime <= arrivalTime && arrivalTime < b.EndTime))
                continue;

            var start = notBefore is { } floor && floor > arrival ? floor : arrival;
            for (var shift = 0; shift <= _planOptions.SlotShiftMaxMin; shift++)
            {
                var slot = start.AddMinutes(shift);
                var count = await _pickupRepository.CountSlotPlansAsync(spot.PickupSpotId, slot, _planOptions.SlotWindowMin,
                    now, excludePlanId, tx, cancellationToken).ConfigureAwait(false);
                if (count < spot.Capacity)
                    return new SlotAssignment(spot.PickupSpotId, slot, spot.WalkMin);
            }
        }

        return null;
    }

    // 계획 응답(설계 6.4). 임의 좌표가 있으면 CUSTOM, 지점이 있으면 SPOT, 둘 다 없으면 pickup·slotDt가 null
    private async Task<PickupPlanDto> BuildPlanDtoAsync(int pickupPlanId, bool reassigned, IDbTransaction? tx,
        CancellationToken cancellationToken)
    {
        var row = await _pickupRepository.GetPlanByIdAsync(pickupPlanId, tx, cancellationToken).ConfigureAwait(false)
                  ?? throw new InvalidOperationException($"pickup plan not found: plan={pickupPlanId}");

        PickupPlanLocationDto? location = null;
        if (row is { CustomLat: { } customLat, CustomLng: { } customLng, CustomName: { } customName })
        {
            location = new PickupPlanLocationDto
            {
                locationType = LocationTypeCustom,
                pickupSpotSeq = null,
                name = customName,
                lat = customLat,
                lng = customLng,
                walkMin = row.WalkMin ?? 0,
                guideText = null,
                photoUrl = null
            };
        }
        else if (row is { PickupSpotId: { } spotId, SpotName: { } spotName, SpotLat: { } spotLat, SpotLng: { } spotLng })
        {
            location = new PickupPlanLocationDto
            {
                locationType = LocationTypeSpot,
                pickupSpotSeq = spotId,
                name = spotName,
                lat = spotLat,
                lng = spotLng,
                walkMin = row.WalkMin ?? 0,
                guideText = row.GuideText,
                photoUrl = row.PhotoUrl
            };
        }

        return new PickupPlanDto
        {
            reason = null,
            pickupPlanSeq = row.PickupPlanId,
            status = row.Status,
            pickupDate = row.PickupDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            endTime = row.EndTime?.ToString(@"hh\:mm"),
            academyName = row.AcademyName,
            pickup = location,
            slotDt = location is null ? null : row.SlotDt,
            expireDt = row.ExpireDt,
            reassigned = reassigned
        };
    }

    // 이벤트 DETAIL의 시각 형식(오프셋 없는 KST)
    private static string? FormatEventDt(DateTime? value)
        => value?.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);

    private static bool IsPlanDuplicateKey(Exception? ex)
    {
        for (; ex is not null; ex = ex.InnerException)
        {
            if (ex is MySqlException { Number: 1062 })
                return true;
        }

        return false;
    }

    private static ResponseModel<PickupPlanDto> PlanOk(PickupPlanDto data)
        => new() { message = "요청이 정상 처리되었습니다.", data = data, code = 200 };

    private static ResponseModel<PickupPlanDto> PlanConflict(string reason)
        => new() { message = "잘못된 요청입니다.", data = new PickupPlanDto { reason = reason }, code = 409 };

    // 대상 픽업. Plan(진행 중 계획)과 Pickup(계획이 없을 때 고른 오늘 픽업) 중 하나만 있다
    private sealed record PickupTarget(LivePlanRow? Plan, TodayPickupRow? Pickup);

    // 기본 지점 배정 결과
    private sealed record SlotAssignment(int PickupSpotId, DateTime SlotDt, byte WalkMin);

    // PLANNED 이벤트 DETAIL(설계 7절). 임의 좌표·이름은 넣지 않고 custom 플래그만 남긴다
    private sealed record PlannedDetail(int? pickupSpotId, bool custom, string? slotDt);
}
