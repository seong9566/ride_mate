using System.Data;
using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Storage;
using MySql.Data.MySqlClient;
using RideMateService.Comm;
using RideMateService.Comm.Dto.v1.Pickup.Request;
using RideMateService.Comm.Dto.v1.Pickup.Response;
using RideMateService.Comm.Map;
using RideMateService.Commons.DBModels;
using RideMateService.Repository.v1.Pickup;

namespace RideMateService.Service.v1.Pickup;

public partial class PickupService
{
    private const string PlanStatusStarted = "STARTED";
    private const string PlanStatusExpired = "EXPIRED";
    private const string AuditSystem = "SYSTEM";
    private const string EventPlanned = "PLANNED";
    private const string EventReassigned = "REASSIGNED";
    private const string ReassignReasonLocationChanged = "LOCATION_CHANGED";
    private const string ReassignReasonLateStart = "LATE_START";
    private const string EventStarted = "STARTED";
    private const int StartEventPastLimitMin = 10;
    private const int StartEventFutureLimitMin = 1;
    private const string LocationTypeSpot = "SPOT";
    private const string LocationTypeCustom = "CUSTOM";
    private const string ReasonTooEarly = "TOO_EARLY";
    private const string ReasonAlreadyStopped = "ALREADY_STOPPED";
    private const string DefaultCustomName = "부모님이 정한 장소";
    private const int PlanAreaRetryMaxAttempts = 3;
    private const int DeadlockMaxAttempts = 3;
    private static readonly string[] LocationChangeableStatuses = { "PLANNED", "STARTED", "WAITING" };

    /// <summary>
    /// 경로 시작. 오늘 계획이 없으면 만들면서 시작하고, 차 도착 예상이 배정 시각보다 늦으면 같은 장소에서 배정 시각을 미룬다(설계 6.3)
    /// </summary>
    public Task<ResponseModel<PickupPlanDto>> UpdatePickupPlanStartService(UpdatePickupPlanStartDto dto,
        CancellationToken cancellationToken = default)
    {
        return RetryOnDeadlockAsync(() => StartPickupPlanCoreAsync(dto, cancellationToken), cancellationToken);
    }

    // 교착은 RetryOnDeadlockAsync가 다시 실행하므로 여기서는 500으로 바꾸지 않고 올려보낸다
    private async Task<ResponseModel<PickupPlanDto>> StartPickupPlanCoreAsync(UpdatePickupPlanStartDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            // 1) 인증 확인 — 로그인 사용자(USER_ID)는 HttpContext의 클레임 sub에서 꺼낸다
            if (!int.TryParse(_httpContextAccessor.HttpContext?.User.FindFirstValue("sub"), out var userId))
                return new ResponseModel<PickupPlanDto> { message = "잘못된 요청입니다.", data = null, code = 401 };

            // 2) 입력 검증 — 필수값, 픽업 좌표는 둘 다 있거나 둘 다 없어야 하고 eventDt는 서버 현재 시각 + 1분을 넘지 않아야 한다. 과거 쪽은 진행 중 계획이 있으면 그 계획의 픽업 날짜, 계획을 새로 만들 때는 서버 현재 시각 −10분으로 본다
            if (dto?.clientEventId is null || dto.eventDt is null || dto.driveMin is null
                || (dto.pickupLat is null) != (dto.pickupLng is null)
                || dto.pickupSpotSeq is < 1
                || (dto.pickupSpotSeq is not null && (dto.pickupLat is not null || dto.pickupLng is not null)))
                return new ResponseModel<PickupPlanDto> { message = "잘못된 요청입니다.", data = null, code = 400 };

            var now = DateTime.Now;
            var eventDt = dto.eventDt.Value;
            if (eventDt > now.AddMinutes(StartEventFutureLimitMin))
                return new ResponseModel<PickupPlanDto> { message = "잘못된 요청입니다.", data = null, code = 400 };

            var clientEventId = dto.clientEventId.Value;
            var driveMin = dto.driveMin.Value;

            // 3) 대상 픽업 — 진행 중 계획이 있으면 그 계획을 시작한다
            var target = await SelectPickupTargetAsync(userId, now, cancellationToken).ConfigureAwait(false);
            if (target is null)
                return new ResponseModel<PickupPlanDto> { message = "요청이 정상 처리되었습니다.", data = null, code = 101 };

            if (target.Plan is { } livePlan)
                return await StartExistingPlanAsync(userId, livePlan.PickupPlanId, clientEventId, eventDt, driveMin,
                    cancellationToken).ConfigureAwait(false);

            // 계획을 새로 만드는 시작만 서버 현재 시각 −10분을 하한으로 둔다
            if (eventDt < now.AddMinutes(-StartEventPastLimitMin))
                return new ResponseModel<PickupPlanDto> { message = "잘못된 요청입니다.", data = null, code = 400 };

            // 4) 잠근 시간표의 학원가가 잠근 학원가와 다르면(그사이 시간표 편집) 대상 고르기부터 다시 한다
            for (var attempt = 1; attempt <= PlanAreaRetryMaxAttempts; attempt++)
            {
                var result = await TryCreateStartedPlanAsync(userId, dto.pickupSpotSeq, dto.pickupLat, dto.pickupLng, clientEventId,
                    eventDt, driveMin, cancellationToken).ConfigureAwait(false);
                if (result is not null)
                    return result;
            }

            _logService.LogMessage($"pickup plan create gave up: area changed or missing {PlanAreaRetryMaxAttempts} times");
            return new ResponseModel<PickupPlanDto> { message = "서버에서 요청을 처리하지 못하였습니다.", data = null, code = 500 };
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested && !IsDeadlock(ex)) // 요청 취소·교착은 그대로 올려보낸다
        {
            _logService.LogMessage(ex.ToString());
#if DEBUG
            _logService.ConsoleWarning(ex.ToString());
#endif
            return new ResponseModel<PickupPlanDto> { message = "서버에서 요청을 처리하지 못하였습니다.", data = null, code = 500 };
        }
    }

    // 시작된 계획 만들기 한 번(설계 6.1 처리 1~7). 잠근 학원가가 틀려 처음부터 다시 해야 하면 null.
    // pickupLat·pickupLng가 있으면 임의 좌표 계획을 만든다(둘 다 있거나 둘 다 없다)
    private async Task<ResponseModel<PickupPlanDto>?> TryCreateStartedPlanAsync(int userId, int? pickupSpotSeq, decimal? pickupLat,
        decimal? pickupLng, Guid clientEventId, DateTime eventDt, int driveMin, CancellationToken cancellationToken)
    {
        var now = DateTime.Now;
        var date = DateOnly.FromDateTime(now);
        var dayStart = date.ToDateTime(TimeOnly.MinValue);
        var leadTime = TimeSpan.FromHours(_planOptions.CreateLeadHours);

        // 1) 대상 픽업 — 진행 중 계획이 있으면 그것을 시작한다(잠금 없이 읽는다)
        var target = await SelectPickupTargetAsync(userId, now, cancellationToken).ConfigureAwait(false);

        // 2) 진행 중 계획도 오늘 남은 픽업도 없다
        if (target is null)
            return new ResponseModel<PickupPlanDto> { message = "요청이 정상 처리되었습니다.", data = null, code = 101 };

        if (target.Plan is { } livePlan)
            return await StartExistingPlanAsync(userId, livePlan.PickupPlanId, clientEventId, eventDt, driveMin,
                cancellationToken).ConfigureAwait(false);

        var pickup = target.Pickup!;

        // 3) 시간 창 — 하원 시각 − CreateLeadHours보다 이르면 아직 만들 수 없다
        if (now < dayStart + pickup.EndTime - leadTime)
            return PlanConflict(ReasonTooEarly);

        // 4) 학원 좌표나 학원가가 없으면 지점을 배정할 수 없어 계획을 만들지 않는다
        if (pickup.AcademyLat is not { } academyLat || pickup.AcademyLng is not { } academyLng
            || pickup.AreaId is not { } areaId)
            return PlanConflict(ReasonNoAcademyLocation);

        // 5) 임의 좌표면 잠그기 전에 외부 호출(카카오 도보 경로·주소 변환)을 끝낸다
        CustomLocation? custom = null;
        if (pickupLat is { } lat && pickupLng is { } lng)
            custom = await ResolveCustomLocationAsync(academyLat, academyLng, lat, lng, cancellationToken).ConfigureAwait(false);

        // 요일 유형 — 그해 공휴일이 적재되지 않았으면 공휴일이 아닌 것으로 본다. 기준 데이터라 잠그기 전에 읽는다
        var holiday = await _pickupRepository.GetHolidayAsync(date, cancellationToken).ConfigureAwait(false);
        var dayType = DepartureGuideCalculator.DayType(date, holiday.IsHoliday);

        int? planIdToStart = null;
        int? duplicateTimetableDayId = null;
        int? createdPlanId = null;
        {
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

            // 6-3) 그사이 다른 요청이 같은 요일의 계획을 만들었으면 롤백 뒤 그 계획을 시작한다
            var existingPlanId = await _pickupRepository.GetLivePlanIdByTimetableDayAsync(day.TimetableDayId, date,
                dbTransaction, cancellationToken).ConfigureAwait(false);
            if (existingPlanId is { } existingId)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                planIdToStart = existingId;
            }
            else
            {
                // 6-4) 차 도착 예상은 잠금을 얻은 뒤에 구한다. 임의 좌표면 아이 도착과 차 도착 예상 중 늦은 시각.
                //      기본 지점이면 차 도착 예상을 하한으로 정원 배정. 쓸 지점이 없으면 지점·시각·도보 시간을 비운 채 만든다
                var carArrival = CeilToMinute(eventDt.AddMinutes(driveMin));
                int? spotId;
                DateTime? slotDt;
                byte? walkMin;
                if (custom is not null)
                {
                    spotId = null;
                    var childArrival = DepartureGuideCalculator.ChildArrival(date, day.EndTime, _options.TidyUpMin,
                        custom.WalkMin);
                    slotDt = carArrival > childArrival ? carArrival : childArrival;
                    walkMin = custom.WalkMin;
                }
                else
                {
                    SlotAssignment? assignment;
                    if (pickupSpotSeq is { } selectedSpotId)
                    {
                        var selected = await AssignSelectedSpotAsync(day.AcademyId, selectedSpotId, date, day.EndTime, dayType, thisTime,
                        null, carArrival, dbTransaction, cancellationToken).ConfigureAwait(false);
                        if (selected.Reason is not null)
                        {
                            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                            return PlanConflict(selected.Reason);
                        }
                        assignment = selected.Assignment;
                    }
                    else
                        assignment = await AssignDefaultSpotAsync(day.AcademyId, day.PreferredSpotId, date, day.EndTime, dayType, thisTime,
                        null, carArrival, dbTransaction, cancellationToken).ConfigureAwait(false);
                    spotId = assignment?.PickupSpotId;
                    slotDt = assignment?.SlotDt;
                    walkMin = assignment?.WalkMin;
                }

                // 6-5) 시작된 계획과 PLANNED·STARTED 이벤트를 넣는다. 감사 컬럼은 Service가 채운다
                var plan = new PickupPlan
                {
                    UserId = userId,
                    ChildId = day.ChildId,
                    TimetableDayId = day.TimetableDayId,
                    AcademyId = day.AcademyId,
                    AreaId = areaId,
                    PickupDate = dayStart,
                    EndTime = day.EndTime,
                    PickupSpotId = spotId,
                    SlotDt = slotDt,
                    WalkMin = (sbyte?)walkMin,
                    CustomLat = custom?.Lat,
                    CustomLng = custom?.Lng,
                    CustomName = custom?.Name,
                    ExpireDt = (slotDt ?? dayStart + day.EndTime).AddHours(_planOptions.ExpireAfterHours),
                    Status = PlanStatusStarted,
                    StartDt = eventDt,
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
                catch (Exception ex) when (IsPlanDuplicateKey(ex)) // 7) UK (TIMETABLE_DAY_ID, PICKUP_DATE) — 롤백 뒤 이미 만들어진 계획을 시작한다
                {
                    _context.ChangeTracker.Clear();
                    await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    duplicateTimetableDayId = day.TimetableDayId;
                }

                if (duplicateTimetableDayId is null)
                {
                    // 임의 좌표의 도보 경로 선은 엔티티에 없는 칸이라 같은 트랜잭션에서 Dapper로 쓴다
                    if (custom?.WalkPathWkt is { } walkPathWkt)
                        await _pickupRepository.SetCustomWalkPathAsync(plan.PickupPlanId, walkPathWkt, dbTransaction,
                            cancellationToken).ConfigureAwait(false);

                    var plannedEvent = new PickupEvent
                    {
                        PickupPlanId = plan.PickupPlanId,
                        EventType = EventPlanned,
                        EventDt = thisTime,
                        ClientEventId = null,
                        Detail = JsonSerializer.Serialize(new PlannedDetail(plan.PickupSpotId, custom is not null,
                            FormatEventDt(plan.SlotDt))),
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

                    var startedEvent = new PickupEvent
                    {
                        PickupPlanId = plan.PickupPlanId,
                        EventType = EventStarted,
                        EventDt = eventDt,
                        ClientEventId = clientEventId,
                        Detail = null,
                        CreateDt = thisTime,
                        CreateUser = AuditUser,
                        UpdateDt = thisTime,
                        UpdateUser = AuditUser,
                        DeleteYn = false
                    };
                    if (!await _pickupRepository.AddPickupEventAsync(startedEvent, cancellationToken).ConfigureAwait(false))
                    {
                        await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                        return new ResponseModel<PickupPlanDto> { message = "서버에서 요청을 처리하지 못하였습니다.", data = null, code = 500 };
                    }

                    await transaction.CommitAsync(CancellationToken.None).ConfigureAwait(false);
                    SignalPlanChanged(plan.PickupPlanId);
                    createdPlanId = plan.PickupPlanId;
                }
            }
        }

        if (planIdToStart is { } existingPlanIdToStart)
            return await StartExistingPlanAsync(userId, existingPlanIdToStart, clientEventId, eventDt, driveMin,
                cancellationToken).ConfigureAwait(false);

        if (duplicateTimetableDayId is { } timetableDayId)
        {
            var duplicatedId = await _pickupRepository.GetLivePlanIdByTimetableDayAsync(timetableDayId, date, null,
                cancellationToken).ConfigureAwait(false);
            if (duplicatedId is null)
            {
                _logService.LogMessage($"pickup plan duplicate key but no plan: timetableDay={timetableDayId}");
                return new ResponseModel<PickupPlanDto> { message = "서버에서 요청을 처리하지 못하였습니다.", data = null, code = 500 };
            }

            return await StartExistingPlanAsync(userId, duplicatedId.Value, clientEventId, eventDt, driveMin,
                cancellationToken).ConfigureAwait(false);
        }

        return PlanOk(await BuildPlanDtoAsync(createdPlanId!.Value, false, null, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// 픽업 장소 바꾸기. 좌표가 오면 임의 좌표로, 둘 다 없으면 기본 지점으로 다시 배정한다(설계 6.2)
    /// </summary>
    public async Task<ResponseModel<PickupPlanDto>> UpdatePickupPlanLocationService(UpdatePickupPlanLocationDto dto,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // 1) 인증 확인 — 로그인 사용자(USER_ID)는 HttpContext의 클레임 sub에서 꺼낸다
            if (!int.TryParse(_httpContextAccessor.HttpContext?.User.FindFirstValue("sub"), out var userId))
                return new ResponseModel<PickupPlanDto> { message = "잘못된 요청입니다.", data = null, code = 401 };

            // 2) 입력 검증 — 필수값, 픽업 좌표는 둘 다 있거나 둘 다 없어야 한다
            if (dto?.pickupPlanSeq is null || dto.clientEventId is null || (dto.pickupLat is null) != (dto.pickupLng is null)
                || dto.pickupSpotSeq is < 1
                || (dto.pickupSpotSeq is not null && (dto.pickupLat is not null || dto.pickupLng is not null)))
                return new ResponseModel<PickupPlanDto> { message = "잘못된 요청입니다.", data = null, code = 400 };

            var pickupPlanId = dto.pickupPlanSeq.Value;
            var clientEventId = dto.clientEventId.Value;

            // 3) 잠금 없이 소유·상태·만료를 먼저 본다(404·409는 외부 호출 전에 돌려준다)
            var plan = await _pickupRepository.GetPlanByIdAsync(pickupPlanId, null, cancellationToken).ConfigureAwait(false);
            if (plan is null)
                return new ResponseModel<PickupPlanDto> { message = "잘못된 요청입니다.", data = null, code = 404 };

            var rejected = CheckPlanLocationChangeable(plan.UserId, plan.Status, plan.ExpireDt, userId, DateTime.Now);
            if (rejected is not null)
                return rejected;

            // 4) 임의 좌표면 잠그기 전에 외부 호출(카카오 도보 경로·주소 변환)을 끝낸다. 학원 좌표는 계획 학원의 현재 값이다
            if (dto.pickupSpotSeq is not null && (plan.AcademyLat is null || plan.AcademyLng is null))
                return PlanConflict(ReasonNoAcademyLocation);

            CustomLocation? custom = null;
            if (dto.pickupLat is { } lat && dto.pickupLng is { } lng)
            {
                if (plan.AcademyLat is not { } academyLat || plan.AcademyLng is not { } academyLng)
                    return PlanConflict(ReasonNoAcademyLocation);

                custom = await ResolveCustomLocationAsync(academyLat, academyLng, lat, lng, cancellationToken).ConfigureAwait(false);
            }

            // 요일 유형(기본 지점으로 되돌릴 때 금지 시간에 쓴다). 기준 데이터라 잠그기 전에 읽는다
            var holiday = await _pickupRepository.GetHolidayAsync(plan.PickupDate, cancellationToken).ConfigureAwait(false);
            var dayType = DepartureGuideCalculator.DayType(plan.PickupDate, holiday.IsHoliday);

            // 5) 트랜잭션 — 예외로 빠져나가면 await using이 트랜잭션을 롤백한다
            var transaction = await _context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await using var transactionScope = transaction.ConfigureAwait(false);
            var dbTransaction = transaction.GetDbTransaction();

            // 5-1) 학원가 행 잠금이 첫 문장이다(재배정은 AREA 먼저). 계획의 AREA_ID는 바뀌지 않는다
            if (!await _pickupRepository.LockAreaAsync(plan.AreaId, dbTransaction, cancellationToken).ConfigureAwait(false))
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                _logService.LogMessage($"pickup plan location area not found: plan={pickupPlanId}");
                return new ResponseModel<PickupPlanDto> { message = "서버에서 요청을 처리하지 못하였습니다.", data = null, code = 500 };
            }

            // 5-2) 계획 행을 PK로 잠그고 잠근 값으로 다시 판정한다
            var locked = await _pickupRepository.LockPickupPlanByIdAsync(pickupPlanId, dbTransaction, cancellationToken)
                .ConfigureAwait(false);
            // 기준 시각은 잠금을 얻은 뒤에 잡는다
            var thisTime = DateTime.Now;
            if (locked is null)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return new ResponseModel<PickupPlanDto> { message = "잘못된 요청입니다.", data = null, code = 404 };
            }

            rejected = CheckPlanLocationChangeable(locked.UserId, locked.Status, locked.ExpireDt, userId, thisTime);
            if (rejected is not null)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return rejected;
            }

            // 5-3) 같은 clientEventId의 재배정이 이미 있으면 같은 요청을 다시 부른 것이다. 지금 계획을 돌려준다.
            //      다른 유형의 이벤트가 그 번호를 쓰고 있으면 잘못된 요청이다(링크 발송과 같다)
            var sameEventType = await _pickupRepository.GetPlanEventTypeAsync(pickupPlanId, clientEventId, dbTransaction,
                cancellationToken).ConfigureAwait(false);
            if (sameEventType is not null)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                if (sameEventType != EventReassigned)
                    return new ResponseModel<PickupPlanDto> { message = "잘못된 요청입니다.", data = null, code = 400 };

                return PlanOk(await BuildPlanDtoAsync(pickupPlanId, false, null, cancellationToken).ConfigureAwait(false));
            }

            var entity = await _pickupRepository.GetPickupPlanEntityAsync(pickupPlanId, cancellationToken).ConfigureAwait(false);
            if (entity is null || locked.EndTime is not { } endTime || locked.AcademyId is not { } academyId)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return new ResponseModel<PickupPlanDto> { message = "잘못된 요청입니다.", data = null, code = 404 };
            }

            // 5-4) 재배정. STARTED면 늦은 출발 결과를 지키려고 지금 배정 시각보다 앞당기지 않는다
            var date = locked.PickupDate;
            var notBefore = locked.Status == PlanStatusStarted ? locked.SlotDt : null;
            if (custom is not null)
            {
                var arrival = DepartureGuideCalculator.ChildArrival(date, endTime, _options.TidyUpMin, custom.WalkMin);
                entity.PickupSpotId = null;
                entity.SlotDt = notBefore is { } floor && floor > arrival ? floor : arrival;
                entity.WalkMin = (sbyte)custom.WalkMin;
                entity.CustomLat = custom.Lat;
                entity.CustomLng = custom.Lng;
                entity.CustomName = custom.Name;
            }
            else
            {
                // 자기 자신의 현재 자리는 정원 세기에서 뺀다. 쓸 지점이 없으면 장소 없는 계획이 된다
                SlotAssignment? assignment;
                if (dto.pickupSpotSeq is { } selectedSpotId)
                {
                    var selected = await AssignSelectedSpotAsync(academyId, selectedSpotId, date, endTime, dayType, thisTime, pickupPlanId,
                    notBefore, dbTransaction, cancellationToken).ConfigureAwait(false);
                    if (selected.Reason is not null)
                    {
                        await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                        return PlanConflict(selected.Reason);
                    }
                    assignment = selected.Assignment;
                }
                else
                    assignment = await AssignDefaultSpotAsync(academyId, plan.PreferredSpotId, date, endTime, dayType, thisTime, pickupPlanId,
                    notBefore, dbTransaction, cancellationToken).ConfigureAwait(false);
                entity.PickupSpotId = assignment?.PickupSpotId;
                entity.SlotDt = assignment?.SlotDt;
                entity.WalkMin = (sbyte?)assignment?.WalkMin;
                entity.CustomLat = null;
                entity.CustomLng = null;
                entity.CustomName = null;
            }

            entity.ExpireDt = (entity.SlotDt ?? date.ToDateTime(TimeOnly.MinValue) + endTime)
                .AddHours(_planOptions.ExpireAfterHours);
            entity.UpdateDt = thisTime;
            entity.UpdateUser = AuditUser;

            if (!await _pickupRepository.UpdatePickupPlanAsync(entity, cancellationToken).ConfigureAwait(false))
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return new ResponseModel<PickupPlanDto> { message = "서버에서 요청을 처리하지 못하였습니다.", data = null, code = 500 };
            }

            // 도보 경로 선은 엔티티에 없는 칸이라 Dapper로 쓴다. 기본 지점으로 되돌리면 비운다
            await _pickupRepository.SetCustomWalkPathAsync(pickupPlanId, custom?.WalkPathWkt, dbTransaction, cancellationToken)
                .ConfigureAwait(false);

            // 5-5) REASSIGNED 이벤트. DETAIL에 좌표·이름은 넣지 않고 custom 플래그만 남긴다
            var reassignedEvent = new PickupEvent
            {
                PickupPlanId = pickupPlanId,
                EventType = EventReassigned,
                EventDt = thisTime,
                ClientEventId = clientEventId,
                Detail = JsonSerializer.Serialize(new ReassignedDetail(ReassignReasonLocationChanged,
                    locked.PickupSpotId, entity.PickupSpotId, locked.CustomLat is not null, custom is not null,
                    FormatEventDt(locked.SlotDt), FormatEventDt(entity.SlotDt))),
                CreateDt = thisTime,
                CreateUser = AuditUser,
                UpdateDt = thisTime,
                UpdateUser = AuditUser,
                DeleteYn = false
            };
            if (!await _pickupRepository.AddPickupEventAsync(reassignedEvent, cancellationToken).ConfigureAwait(false))
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return new ResponseModel<PickupPlanDto> { message = "서버에서 요청을 처리하지 못하였습니다.", data = null, code = 500 };
            }

            await transaction.CommitAsync(CancellationToken.None).ConfigureAwait(false);
            SignalPlanChanged(pickupPlanId);

            return PlanOk(await BuildPlanDtoAsync(pickupPlanId, true, null, cancellationToken).ConfigureAwait(false));
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

    // 진행 중 계획을 시작한다. 계획을 STARTED로 바꾸고, 차 도착 예상이 배정 시각보다 늦으면 같은 장소에서 배정 시각을 미룬다
    private async Task<ResponseModel<PickupPlanDto>> StartExistingPlanAsync(int userId, int pickupPlanId,
        Guid clientEventId, DateTime eventDt, int driveMin, CancellationToken cancellationToken)
    {
        var now = DateTime.Now;

        // 3) 잠금 없이 소유·상태·만료를 먼저 본다. 이미 시작했으면 잠그지 않고 지금 계획을 돌려준다(두 번 눌러도 한 번)
        var plan = await _pickupRepository.GetPlanByIdAsync(pickupPlanId, null, cancellationToken).ConfigureAwait(false);
        if (plan is null)
            return new ResponseModel<PickupPlanDto> { message = "잘못된 요청입니다.", data = null, code = 404 };

        // 진행 중 계획의 시작은 계획의 픽업 날짜 00:00이 하한이다. 시작 기록이 있으면 잠근 뒤 그 시각(START_DT)으로 다시 본다
        if (IsBeforePlanDay(eventDt, plan.PickupDate))
            return new ResponseModel<PickupPlanDto> { message = "잘못된 요청입니다.", data = null, code = 400 };

        var rejected = CheckPlanLocationChangeable(plan.UserId, plan.Status, plan.ExpireDt, userId, now);
        if (rejected is not null)
            return rejected;

        if (plan.Status == PlanStatusStarted)
            return PlanOk(await BuildPlanDtoAsync(pickupPlanId, false, null, cancellationToken).ConfigureAwait(false));

        // 4) 트랜잭션 — 예외로 빠져나가면 await using이 트랜잭션을 롤백한다
        var transaction = await _context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using var transactionScope = transaction.ConfigureAwait(false);
        var dbTransaction = transaction.GetDbTransaction();

        // 4-1) 재배정이 있을 수 있으므로 학원가 행 잠금이 첫 문장이다(재배정은 AREA 먼저). 계획의 AREA_ID는 바뀌지 않는다
        if (!await _pickupRepository.LockAreaAsync(plan.AreaId, dbTransaction, cancellationToken).ConfigureAwait(false))
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            _logService.LogMessage($"pickup plan start area not found: plan={pickupPlanId}");
            return new ResponseModel<PickupPlanDto> { message = "서버에서 요청을 처리하지 못하였습니다.", data = null, code = 500 };
        }

        // 4-2) 계획 행을 PK로 잠그고 잠근 값으로 다시 판정한다
        var locked = await _pickupRepository.LockPickupPlanByIdAsync(pickupPlanId, dbTransaction, cancellationToken)
            .ConfigureAwait(false);
        // 기준 시각은 잠금을 얻은 뒤에 잡는다
        var thisTime = DateTime.Now;
        if (locked is null)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return new ResponseModel<PickupPlanDto> { message = "잘못된 요청입니다.", data = null, code = 404 };
        }

        rejected = CheckPlanLocationChangeable(locked.UserId, locked.Status, locked.ExpireDt, userId, thisTime);
        if (rejected is not null)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return rejected;
        }

        // 그사이 다른 요청이 시작했으면 지금 계획을 돌려준다
        if (locked.Status == PlanStatusStarted)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return PlanOk(await BuildPlanDtoAsync(pickupPlanId, false, null, cancellationToken).ConfigureAwait(false));
        }

        // 4-3) 같은 clientEventId의 시작 이벤트가 이미 있으면 같은 요청을 다시 부른 것이다. 지금 계획을 돌려준다.
        //      다른 유형의 이벤트가 그 번호를 쓰고 있으면 잘못된 요청이다(링크 발송과 같다)
        var sameEventType = await _pickupRepository.GetPlanEventTypeAsync(pickupPlanId, clientEventId, dbTransaction,
            cancellationToken).ConfigureAwait(false);
        if (sameEventType is not null)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            if (sameEventType != EventStarted)
                return new ResponseModel<PickupPlanDto> { message = "잘못된 요청입니다.", data = null, code = 400 };

            return PlanOk(await BuildPlanDtoAsync(pickupPlanId, false, null, cancellationToken).ConfigureAwait(false));
        }

        var entity = await _pickupRepository.GetPickupPlanEntityAsync(pickupPlanId, cancellationToken).ConfigureAwait(false);
        if (entity is null || locked.EndTime is not { } endTime)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return new ResponseModel<PickupPlanDto> { message = "잘못된 요청입니다.", data = null, code = 404 };
        }

        // 진행 중 계획의 시작은 그 계획의 시작 시각(START_DT) 이후만 받는다. 시작 요청에는 계획 ID가 없어,
        // 끝난 다른 계획의 늦은 재전송이 이 계획을 다시 시작하고 START_DT를 과거로 덮지 않게 한다
        if (entity.StartDt is { } startedAt && eventDt < startedAt)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return new ResponseModel<PickupPlanDto> { message = "잘못된 요청입니다.", data = null, code = 400 };
        }

        entity.Status = PlanStatusStarted;
        entity.StartDt = eventDt;

        // 4-4) 늦은 출발 — 차 도착 예상(eventDt + driveMin, 분 단위 올림)이 배정 시각보다 늦으면 같은 장소에서 미룬다.
        //      배정 장소가 없는 계획(SLOT_DT 없음)은 재배정하지 않는다
        var carArrival = CeilToMinute(eventDt.AddMinutes(driveMin));
        var reassigned = locked.SlotDt is { } currentSlot && carArrival > currentSlot;
        if (reassigned)
        {
            if (locked.CustomLat is not null || locked.PickupSpotId is not { } spotId)
            {
                // 임의 좌표는 정원이 없다
                entity.SlotDt = carArrival;
            }
            else
            {
                // 기본 지점 — 차 도착 예상부터 SlotShiftMaxMin까지 1분씩 늦춰 정원이 남은 첫 시각. 자기 자리는 빼고 센다.
                // 자리가 없어도 다른 지점으로 옮기지 않고 차 도착 예상 시각으로 배정한다(정원 초과 허용, 설계 6.3)
                var capacity = await _pickupRepository.GetPickupSpotCapacityAsync(spotId, dbTransaction,
                    cancellationToken).ConfigureAwait(false) ?? 0;
                DateTime? freeSlot = null;
                for (var shift = 0; shift <= _planOptions.SlotShiftMaxMin; shift++)
                {
                    var slot = carArrival.AddMinutes(shift);
                    var count = await _pickupRepository.CountSlotPlansAsync(spotId, slot, _planOptions.SlotWindowMin,
                        thisTime, pickupPlanId, dbTransaction, cancellationToken).ConfigureAwait(false);
                    if (count < capacity)
                    {
                        freeSlot = slot;
                        break;
                    }
                }

                if (freeSlot is null)
                    _logService.LogMessage($"late start over capacity: plan={pickupPlanId}");

                entity.SlotDt = freeSlot ?? carArrival;
            }

            entity.ExpireDt = entity.SlotDt.Value.AddHours(_planOptions.ExpireAfterHours);
        }

        entity.UpdateDt = thisTime;
        entity.UpdateUser = AuditUser;

        if (!await _pickupRepository.UpdatePickupPlanAsync(entity, cancellationToken).ConfigureAwait(false))
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return new ResponseModel<PickupPlanDto> { message = "서버에서 요청을 처리하지 못하였습니다.", data = null, code = 500 };
        }

        // 4-5) STARTED 이벤트(EVENT_DT = 차량 출발 시각), 재배정했으면 그다음에 REASSIGNED 이벤트
        var startedEvent = new PickupEvent
        {
            PickupPlanId = pickupPlanId,
            EventType = EventStarted,
            EventDt = eventDt,
            ClientEventId = clientEventId,
            Detail = null,
            CreateDt = thisTime,
            CreateUser = AuditUser,
            UpdateDt = thisTime,
            UpdateUser = AuditUser,
            DeleteYn = false
        };
        if (!await _pickupRepository.AddPickupEventAsync(startedEvent, cancellationToken).ConfigureAwait(false))
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return new ResponseModel<PickupPlanDto> { message = "서버에서 요청을 처리하지 못하였습니다.", data = null, code = 500 };
        }

        if (reassigned)
        {
            // 늦은 출발 재배정은 출발이 낳은 것이라 EVENT_DT를 출발 시각에 맞춘다. 차량 이벤트 번호는 STARTED가 쓴다
            var custom = locked.CustomLat is not null;
            var reassignedEvent = new PickupEvent
            {
                PickupPlanId = pickupPlanId,
                EventType = EventReassigned,
                EventDt = eventDt,
                ClientEventId = null,
                Detail = JsonSerializer.Serialize(new ReassignedDetail(ReassignReasonLateStart,
                    locked.PickupSpotId, entity.PickupSpotId, custom, custom,
                    FormatEventDt(locked.SlotDt), FormatEventDt(entity.SlotDt))),
                CreateDt = thisTime,
                CreateUser = AuditUser,
                UpdateDt = thisTime,
                UpdateUser = AuditUser,
                DeleteYn = false
            };
            if (!await _pickupRepository.AddPickupEventAsync(reassignedEvent, cancellationToken).ConfigureAwait(false))
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return new ResponseModel<PickupPlanDto> { message = "서버에서 요청을 처리하지 못하였습니다.", data = null, code = 500 };
            }
        }

        await transaction.CommitAsync(CancellationToken.None).ConfigureAwait(false);
        SignalPlanChanged(pickupPlanId);

        return PlanOk(await BuildPlanDtoAsync(pickupPlanId, reassigned, null, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// 만료 주기 작업 한 회차(설계 10절). EXPIRE_DT가 지난 진행 중 계획을 계획마다 따로 트랜잭션으로 EXPIRED로 끝내고 익명화한다.
    /// 끝낸 계획 수를 돌려준다. HTTP 요청 밖에서 부르므로 HttpContext를 쓰지 않는다
    /// </summary>
    public async Task<int> ExpirePickupPlansService(CancellationToken cancellationToken = default)
    {
        // 1) 후보는 일반 조회로 찾는다. 끝낼지는 계획마다 잠근 뒤 다시 본다
        var candidateIds = await _pickupRepository.GetExpiredPlanIdsAsync(DateTime.Now, cancellationToken)
            .ConfigureAwait(false);

        var expired = 0;
        foreach (var pickupPlanId in candidateIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                if (await ExpirePickupPlanAsync(pickupPlanId, cancellationToken).ConfigureAwait(false))
                    expired++;
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested) // 한 계획의 오류는 그 계획만 건너뛴다
            {
                // 계획 ID만 남긴다. 예외 본문은 Repository가 이미 남겼다
                _logService.LogMessage($"pickup plan expire failed: plan={pickupPlanId}, {ex.GetType().Name}");
            }
        }

        return expired;
    }

    // 계획 하나를 만료로 끝낸다. 잠근 값으로 이미 끝났거나 만료 시각이 미뤄졌으면(늦은 출발 재배정) 건너뛰고 false
    private async Task<bool> ExpirePickupPlanAsync(int pickupPlanId, CancellationToken cancellationToken)
    {
        // 2) 트랜잭션 — 예외로 빠져나가면 await using이 트랜잭션을 롤백한다
        var transaction = await _context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using var transactionScope = transaction.ConfigureAwait(false);
        var dbTransaction = transaction.GetDbTransaction();

        // 2-1) 계획 행을 PK로 잠그는 것이 첫 문장이다(계획 끝내기 = 계획 PK → TIMETABLE_DAY → UPDATE)
        var locked = await _pickupRepository.LockPickupPlanByIdAsync(pickupPlanId, dbTransaction, cancellationToken)
            .ConfigureAwait(false);
        // 기준 시각은 잠금을 얻은 뒤에 잡는다. 잠금을 기다린 사이 재배정이 만료 시각을 미뤘을 수 있다
        var thisTime = DateTime.Now;
        if (locked is null || ClosedPlanStatuses.Contains(locked.Status) || locked.ExpireDt > thisTime)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return false;
        }

        // 2-2) 끝내기와 익명화, LAST_PICKUP_DATE 반영, 링크·이벤트 정리, EXPIRED 이벤트
        if (!await _pickupRepository.ClosePickupPlanAsync(pickupPlanId, PlanStatusExpired, true, thisTime, AuditSystem,
                dbTransaction, cancellationToken).ConfigureAwait(false))
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return false;
        }

        await transaction.CommitAsync(CancellationToken.None).ConfigureAwait(false);
        SignalPlanEnded(pickupPlanId);
        return true;
    }

    // 분 단위 올림. 초 이하가 0이 아니면 다음 분이다
    private static DateTime CeilToMinute(DateTime value)
    {
        var remainder = value.Ticks % TimeSpan.TicksPerMinute;
        return remainder == 0 ? value : value.AddTicks(TimeSpan.TicksPerMinute - remainder);
    }

    // 장소를 바꾸거나 경로를 시작할 수 있는지(설계 6.2·6.3). 남의 계획·끝난 계획(USER_ID NULL)은 404, 정차·한 바퀴 중이면 409 ALREADY_STOPPED,
    // PLANNED·STARTED·WAITING이 아니면 404, 만료 시각이 지났으면 409 PLAN_CLOSED. 바꿀 수 있으면 null
    private static ResponseModel<PickupPlanDto>? CheckPlanLocationChangeable(int? ownerId, string status, DateTime expireDt,
        int userId, DateTime now)
    {
        if (ownerId != userId)
            return new ResponseModel<PickupPlanDto> { message = "잘못된 요청입니다.", data = null, code = 404 };

        if (StoppedPlanStatuses.Contains(status))
            return PlanConflict(ReasonAlreadyStopped);

        if (!LocationChangeableStatuses.Contains(status))
            return new ResponseModel<PickupPlanDto> { message = "잘못된 요청입니다.", data = null, code = 404 };

        if (expireDt <= now)
            return PlanConflict(ReasonPlanClosed);

        return null;
    }

    // 임의 좌표의 도보 시간·경로와 장소 이름(설계 6.1 처리 5, 8절). 잠그기 전에 부른다.
    // 카카오 도보 경로가 실패하면 직선 거리 ÷ 아이 도보 속도(올림)로 추정하고 경로는 없다. 이름 변환이 실패하면 기본 이름을 쓴다
    private async Task<CustomLocation> ResolveCustomLocationAsync(decimal academyLat, decimal academyLng, decimal lat,
        decimal lng, CancellationToken cancellationToken)
    {
        int minutes;
        string? walkPathWkt = null;
        var route = await _walkRouteClient.GetWalkRouteAsync(academyLat, academyLng, lat, lng, cancellationToken)
            .ConfigureAwait(false);
        if (route is not null)
        {
            minutes = DepartureGuideCalculator.SecondsToMinutesCeil(route.TotalSeconds);
            // LINESTRING은 점이 둘 이상이어야 한다. SRID 4326 WKT라 "위도 경도" 순이다(ParseLineString과 같다)
            if (route.Path.Count >= 2)
                walkPathWkt = "LINESTRING(" + string.Join(",",
                    route.Path.Select(p => FormattableString.Invariant($"{p.Lat} {p.Lng}"))) + ")";
        }
        else
        {
            var meters = GeoDistance.Meters(academyLat, academyLng, lat, lng);
            minutes = DepartureGuideCalculator.EstimateMinutes(meters, _options.ChildWalkKmh);
        }

        // WALK_MIN은 TINYINT(부호 있음)라 127분을 넘지 않게 한다. 학원과의 거리는 검증하지 않으므로 먼 좌표가 올 수 있다
        var walkMin = (byte)Math.Min(minutes, sbyte.MaxValue);

        var name = await _addressClient.GetPlaceNameAsync(lat, lng, cancellationToken).ConfigureAwait(false)
                   ?? DefaultCustomName;

        return new CustomLocation(lat, lng, name, walkMin, walkPathWkt);
    }

    // 대상 픽업 고르기(설계 3절). 출발 안내와 계획 API가 같은 규칙을 쓴다.
    // 1) 진행 중 계획(서버 시각 now 기준)이 있으면 그것. 여럿이면 배정 시각(없으면 하원 시각)이 가장 이른 것
    // 2) 없으면 now 날짜의 오늘 픽업 중 하원 + PickupWindowHours가 now보다 늦은 것에서 하원이 가장 이른 것
    private async Task<PickupTarget?> SelectPickupTargetAsync(int userId, DateTime now,
        CancellationToken cancellationToken)
    {
        var livePlan = await _pickupRepository.GetLivePlanAsync(userId, DateOnly.FromDateTime(now), now,
            cancellationToken).ConfigureAwait(false);
        if (livePlan is not null)
            return new PickupTarget(livePlan, null);

        var date = DateOnly.FromDateTime(now);
        var todays = await _pickupRepository.GetTodayPickupsAsync(userId, date,
            DepartureGuideCalculator.DayOfWeek(date), cancellationToken).ConfigureAwait(false);

        var dayStart = date.ToDateTime(TimeOnly.MinValue);
        var window = TimeSpan.FromHours(_options.PickupWindowHours);
        var pickup = todays
            .Where(t => dayStart + t.EndTime + window > now)
            .OrderBy(t => t.EndTime)
            .ThenBy(t => t.TimetableDayId)
            .FirstOrDefault();

        return pickup is null ? null : new PickupTarget(null, pickup);
    }

    // 기본 지점 배정(설계 6.1 "기본 지점 배정 순서"). 우리 픽업 장소(preferredSpotId)가 후보면 맨 앞에, 나머지는 PRIORITY 순으로 보며(USE_YN = 0은 조회에서 빠진다) [ADR-0025]
    // 아이 도착 시각이 금지 시간 [시작, 종료)에 걸리면 건너뛴다. 남은 지점마다 시작 시각 = max(아이 도착, notBefore)부터
    // SlotShiftMaxMin까지 1분씩 늦춰 정원이 남은 첫 시각을 쓴다. 늦춘 시각은 금지 시간을 다시 보지 않는다. 없으면 null
    private async Task<SlotAssignment?> AssignDefaultSpotAsync(int academyId, int? preferredSpotId, DateOnly date, TimeSpan endTime,
        string dayType, DateTime now, int? excludePlanId, DateTime? notBefore, IDbTransaction tx,
        CancellationToken cancellationToken)
    {
        var spots = await _pickupRepository.GetAcademyPickupSpotsAsync(academyId, preferredSpotId, cancellationToken)
            .ConfigureAwait(false);
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

    // 부모가 오늘 고른 지점만 배정한다. 실패해도 다른 후보로 넘기지 않는다.
    private async Task<(SlotAssignment? Assignment, string? Reason)> AssignSelectedSpotAsync(
        int academyId, int selectedSpotId, DateOnly date, TimeSpan endTime, string dayType,
        DateTime now, int? excludePlanId, DateTime? notBefore, IDbTransaction tx, CancellationToken cancellationToken)
    {
        var spots = await _pickupRepository.GetAcademyPickupSpotsAsync(academyId, null, cancellationToken).ConfigureAwait(false);
        var spot = spots.FirstOrDefault(s => s.PickupSpotId == selectedSpotId);
        if (spot is null)
            return (null, "INVALID_PICKUP_SPOT");
        var blocks = await _pickupRepository.GetPickupSpotBlocksAsync(new[] { selectedSpotId }, dayType,
            cancellationToken).ConfigureAwait(false);
        var arrival = DepartureGuideCalculator.ChildArrival(date, endTime, _options.TidyUpMin, spot.WalkMin);
        if (blocks.Any(b => b.StartTime <= arrival.TimeOfDay && arrival.TimeOfDay < b.EndTime))
            return (null, "SPOT_UNAVAILABLE");
        var start = notBefore is { } floor && floor > arrival ? floor : arrival;
        for (var shift = 0; shift <= _planOptions.SlotShiftMaxMin; shift++)
        {
            var slot = start.AddMinutes(shift);
            var count = await _pickupRepository.CountSlotPlansAsync(selectedSpotId, slot, _planOptions.SlotWindowMin,
                now, excludePlanId, tx, cancellationToken).ConfigureAwait(false);
            if (count < spot.Capacity)
                return (new SlotAssignment(selectedSpotId, slot, spot.WalkMin), null);
        }
        return (null, "SPOT_FULL");
    }

    // 계획 응답(설계 6.4). 임의 좌표가 있으면 CUSTOM, 지점이 있으면 SPOT, 둘 다 없으면 pickup·slotDt가 null.
    // parkingLotSeq는 STATUS가 WAITING일 때만 채운다(설계 4.5, 그 밖에는 null)
    private async Task<PickupPlanDto> BuildPlanDtoAsync(int pickupPlanId, bool reassigned, IDbTransaction? tx,
        CancellationToken cancellationToken)
    {
        var row = await _pickupRepository.GetPlanByIdAsync(pickupPlanId, tx, cancellationToken).ConfigureAwait(false)
                  ?? throw new InvalidOperationException($"pickup plan not found: plan={pickupPlanId}");

        var location = ToPlanLocation(row);

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
            reassigned = reassigned,
            // 대기 중(WAITING)일 때만 채운다. 출발하면 DB에는 마지막 주차장이 남지만 응답은 null이다
            parkingLotSeq = row.Status == PlanStatusWaiting ? row.ParkingLotId : null
        };
    }

    // 계획의 픽업 장소를 임의 좌표 우선으로 응답에 옮긴다
    private static PickupPlanLocationDto? ToPlanLocation(LivePlanRow row)
    {
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

        return location;
    }

    // 이벤트 DETAIL의 시각 형식(오프셋 없는 KST)
    private static string? FormatEventDt(DateTime? value)
        => value?.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);

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
                    _logService.LogMessage($"pickup deadlock, retrying: attempt={attempt + 1}/{DeadlockMaxAttempts}");
                    // 동시에 진 요청들이 같은 순간에 다시 잠금을 잡지 않도록 무작위로 기다린다
                    await Task.Delay(Random.Shared.Next(10, 50), cancellationToken).ConfigureAwait(false);
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

    // REASSIGNED 이벤트 DETAIL(설계 7절). 임의 좌표·이름은 넣지 않고 custom 플래그만 남긴다
    private sealed record ReassignedDetail(string reason, int? fromSpotId, int? toSpotId, bool fromCustom, bool toCustom,
        string? fromSlotDt, string? toSlotDt);

    // 임의 좌표 외부 호출 결과. WalkPathWkt는 SRID 4326 WKT("LINESTRING(위도 경도,…)"), 도보 경로가 실패했으면 null
    private sealed record CustomLocation(decimal Lat, decimal Lng, string Name, byte WalkMin, string? WalkPathWkt);
}
