using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Storage;
using RideMateService.Comm;
using RideMateService.Comm.Dto.v1.Pickup.Request;
using RideMateService.Comm.Dto.v1.Pickup.Response;
using RideMateService.Comm.Map;
using RideMateService.Commons.DBModels;
using RideMateService.Repository.v1.Pickup;

namespace RideMateService.Service.v1.Pickup;

public partial class PickupService
{
    private const string PlanStatusStopped = "STOPPED";
    private const string PlanStatusLooping = "LOOPING";
    private const string EventStopped = "STOPPED";
    private const string EventWrongStop = "WRONG_STOP";
    private const string ReasonInvalidStatus = "INVALID_STATUS";
    private const string PlanStatusBoarded = "BOARDED";
    private const string EventBoarded = "BOARDED";
    private static readonly string[] StopEventTypes = { EventStopped, EventWrongStop };
    private static readonly string[] BoardEventTypes = { EventBoarded };
    private static readonly string[] StoppableStatuses = { "PLANNED", "STARTED", "WAITING", "STOPPED" };

    /// <summary>
    /// 정차 기록(설계 4.2). 배정 장소와의 거리와 정차 위치의 단속 구역을 판정해 STOPPED 또는 WRONG_STOP 이벤트를 넣고 계획을 STOPPED로 바꾼다
    /// </summary>
    public Task<ResponseModel<PickupPlanDto>> UpdatePickupPlanStopService(UpdatePickupPlanStopDto dto,
        CancellationToken cancellationToken = default)
    {
        return RetryOnDeadlockAsync(() => UpdatePickupPlanStopCoreAsync(dto, cancellationToken), cancellationToken);
    }

    // 교착은 RetryOnDeadlockAsync가 다시 실행하므로 여기서는 500으로 바꾸지 않고 올려보낸다
    private async Task<ResponseModel<PickupPlanDto>> UpdatePickupPlanStopCoreAsync(UpdatePickupPlanStopDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            // 1) 인증 확인 — 로그인 사용자(USER_ID)는 HttpContext의 클레임 sub에서 꺼낸다
            if (!int.TryParse(_httpContextAccessor.HttpContext?.User.FindFirstValue("sub"), out var userId))
                return new ResponseModel<PickupPlanDto> { message = "잘못된 요청입니다.", data = null, code = 401 };

            // 2) 입력 검증 — 필수값, eventDt는 서버 현재 시각 기준 -10분 ~ +1분 안이어야 한다(경로 시작과 같다)
            if (dto?.pickupPlanSeq is null || dto.clientEventId is null || dto.eventDt is null || dto.lat is null
                || dto.lng is null)
                return new ResponseModel<PickupPlanDto> { message = "잘못된 요청입니다.", data = null, code = 400 };

            var now = DateTime.Now;
            var eventDt = dto.eventDt.Value;
            if (eventDt < now.AddMinutes(-StartEventPastLimitMin) || eventDt > now.AddMinutes(StartEventFutureLimitMin))
                return new ResponseModel<PickupPlanDto> { message = "잘못된 요청입니다.", data = null, code = 400 };

            var pickupPlanId = dto.pickupPlanSeq.Value;
            var clientEventId = dto.clientEventId.Value;
            var lat = dto.lat.Value;
            var lng = dto.lng.Value;

            // 3) 잠금 없이 소유·상태·만료를 먼저 본다. 없거나 끝난 계획이면 같은 요청의 재전송인지 보고, 아니면 404
            var plan = await _pickupRepository.GetPlanByIdAsync(pickupPlanId, null, cancellationToken).ConfigureAwait(false);
            if (plan is null || plan.UserId is null || ClosedPlanStatuses.Contains(plan.Status))
                return await TryReplayEndedAsync(plan, pickupPlanId, clientEventId, StopEventTypes, cancellationToken)
                           .ConfigureAwait(false)
                       ?? new ResponseModel<PickupPlanDto> { message = "잘못된 요청입니다.", data = null, code = 404 };

            if (plan.UserId != userId)
                return new ResponseModel<PickupPlanDto> { message = "잘못된 요청입니다.", data = null, code = 404 };

            // 같은 요청의 재전송은 만료·상태 409보다 먼저 본다. 만료 시각이 지났지만 만료 작업이 아직 끝내지 않은 사이에도 200이다
            var sameEvent = await _pickupRepository.GetPlanEventByClientIdAsync(pickupPlanId, clientEventId,
                cancellationToken).ConfigureAwait(false);
            if (sameEvent is not null && StopEventTypes.Contains(sameEvent.EventType))
                return await ReplayLiveStopAsync(pickupPlanId, sameEvent.EventType, sameEvent.Detail, cancellationToken)
                    .ConfigureAwait(false);

            var rejected = CheckPlanStoppable(plan.UserId, plan.Status, plan.ExpireDt, userId, now);
            if (rejected is not null)
                return rejected;

            // 4) 단속 판정은 DB 읽기뿐이라 잠그기 전에 한다. 출발 안내 9단계와 같은 규칙이고 판정 시각은 eventDt다.
            //    학원가(AREA_ID)는 계획에서 바뀌지 않으므로 잠근 뒤 다시 보지 않는다
            var eventDate = DateOnly.FromDateTime(eventDt);
            var holiday = await _pickupRepository.GetHolidayAsync(eventDate, cancellationToken).ConfigureAwait(false);
            var dayType = DepartureGuideCalculator.DayType(eventDate, holiday.IsHoliday);
            var zones = await MatchZonesAsync(plan.AreaId, lat, lng, dayType, eventDt.TimeOfDay, cancellationToken)
                .ConfigureAwait(false);
            string enforcement;
            if (zones is null)
                enforcement = EnforcementUnknown;
            else if (zones.Count > 0)
                enforcement = EnforcementRestricted;
            else if (!holiday.YearLoaded)
                enforcement = EnforcementUnknown;
            else
                enforcement = EnforcementClear;

            // 5) 트랜잭션 — 예외로 빠져나가면 await using이 트랜잭션을 롤백한다
            var transaction = await _context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await using var transactionScope = transaction.ConfigureAwait(false);
            var dbTransaction = transaction.GetDbTransaction();

            // 5-1) 계획 행을 PK로 잠그는 것이 첫 문장이다(재배정이 없어 AREA는 잠그지 않는다). 잠근 값으로 다시 판정한다
            var locked = await _pickupRepository.LockPickupPlanByIdAsync(pickupPlanId, dbTransaction, cancellationToken)
                .ConfigureAwait(false);
            // 기준 시각은 잠금을 얻은 뒤에 잡는다
            var thisTime = DateTime.Now;
            if (locked is null)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return new ResponseModel<PickupPlanDto> { message = "잘못된 요청입니다.", data = null, code = 404 };
            }

            // 잠금을 기다리는 사이 계획이 끝났으면(탑승·취소·만료) 같은 요청의 재전송인지 본다
            if (locked.UserId is null || ClosedPlanStatuses.Contains(locked.Status))
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                var ended = await _pickupRepository.GetPlanByIdAsync(pickupPlanId, null, cancellationToken).ConfigureAwait(false);
                return await TryReplayEndedAsync(ended, pickupPlanId, clientEventId, StopEventTypes, cancellationToken)
                           .ConfigureAwait(false)
                       ?? new ResponseModel<PickupPlanDto> { message = "잘못된 요청입니다.", data = null, code = 404 };
            }

            if (locked.UserId != userId)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return new ResponseModel<PickupPlanDto> { message = "잘못된 요청입니다.", data = null, code = 404 };
            }

            // 5-2) 같은 clientEventId의 정차 이벤트가 이미 있으면 같은 요청을 다시 부른 것이다. 만료·상태 409보다 먼저 보고,
            //      지금 계획과 처음 이벤트로 만든 판정을 돌려준다
            var sameEventType = await _pickupRepository.GetPlanEventTypeAsync(pickupPlanId, clientEventId, dbTransaction,
                cancellationToken).ConfigureAwait(false);
            if (sameEventType is not null && StopEventTypes.Contains(sameEventType))
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                var replayEvent = await _pickupRepository.GetPlanEventByClientIdAsync(pickupPlanId, clientEventId,
                    cancellationToken).ConfigureAwait(false);
                return await ReplayLiveStopAsync(pickupPlanId, sameEventType, replayEvent?.Detail, cancellationToken)
                    .ConfigureAwait(false);
            }

            rejected = CheckPlanStoppable(locked.UserId, locked.Status, locked.ExpireDt, userId, thisTime);
            if (rejected is not null)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return rejected;
            }

            // 다른 유형의 이벤트가 그 번호를 쓰고 있으면 잘못된 요청이다
            if (sameEventType is not null)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return new ResponseModel<PickupPlanDto> { message = "잘못된 요청입니다.", data = null, code = 400 };
            }

            // 5-3) 배정 장소는 잠근 뒤에 읽는다. 잠금을 기다린 사이 장소 바꾸기가 커밋했을 수 있다
            var current = await _pickupRepository.GetPlanByIdAsync(pickupPlanId, dbTransaction, cancellationToken)
                .ConfigureAwait(false);
            var entity = await _pickupRepository.GetPickupPlanEntityAsync(pickupPlanId, cancellationToken).ConfigureAwait(false);
            if (current is null || entity is null)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return new ResponseModel<PickupPlanDto> { message = "잘못된 요청입니다.", data = null, code = 404 };
            }

            // 장소는 기본 지점이 있으면 지점 좌표, 없으면 임의 좌표다. 둘 다 없으면 비교할 장소가 없다
            var placeLat = current.PickupSpotId is not null ? current.SpotLat : current.CustomLat;
            var placeLng = current.PickupSpotId is not null ? current.SpotLng : current.CustomLng;
            int? distanceM = null;
            if (placeLat is { } spotLat && placeLng is { } spotLng)
                distanceM = (int)Math.Round(GeoDistance.Meters(spotLat, spotLng, lat, lng));
            var atSpot = distanceM <= _planOptions.StopRadiusM;

            var stopCheck = new PickupStopCheckDto
            {
                atSpot = atSpot,
                distanceM = distanceM,
                enforcement = enforcement,
                zoneTypes = enforcement == EnforcementRestricted ? zones! : new List<string>()
            };

            // 5-4) STATUS = STOPPED. STOP_DT는 위치와 상관없이 처음 정차한 시각이라 비어 있을 때만 채운다
            entity.Status = PlanStatusStopped;
            entity.StopDt ??= eventDt;
            entity.UpdateDt = thisTime;
            entity.UpdateUser = AuditUser;

            if (!await _pickupRepository.UpdatePickupPlanAsync(entity, cancellationToken).ConfigureAwait(false))
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return new ResponseModel<PickupPlanDto> { message = "서버에서 요청을 처리하지 못하였습니다.", data = null, code = 500 };
            }

            // 5-5) 장소가 있고 반경 밖이면 WRONG_STOP(DETAIL에 걸린 구역 유형, 판정 불가면 null), 아니면 STOPPED.
            //      장소가 없는 계획은 비교할 장소가 없어 STOPPED다. 좌표와 거리는 넣지 않는다
            var wrongStop = distanceM is not null && !atSpot;
            var stopEvent = new PickupEvent
            {
                PickupPlanId = pickupPlanId,
                EventType = wrongStop ? EventWrongStop : EventStopped,
                EventDt = eventDt,
                ClientEventId = clientEventId,
                Detail = wrongStop ? JsonSerializer.Serialize(new WrongStopDetail(zones)) : null,
                CreateDt = thisTime,
                CreateUser = AuditUser,
                UpdateDt = thisTime,
                UpdateUser = AuditUser,
                DeleteYn = false
            };
            if (!await _pickupRepository.AddPickupEventAsync(stopEvent, cancellationToken).ConfigureAwait(false))
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return new ResponseModel<PickupPlanDto> { message = "서버에서 요청을 처리하지 못하였습니다.", data = null, code = 500 };
            }

            await transaction.CommitAsync(CancellationToken.None).ConfigureAwait(false);

            var result = await BuildPlanDtoAsync(pickupPlanId, false, null, cancellationToken).ConfigureAwait(false);
            result.stopCheck = stopCheck;
            return PlanOk(result);
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

    /// <summary>
    /// 탑승 기록(설계 4.3). BOARD_DT를 쓰고 계획을 BOARDED로 끝낸다(익명화, LAST_PICKUP_DATE, 링크 종료, BOARDED 이벤트)
    /// </summary>
    public Task<ResponseModel<PickupPlanDto>> UpdatePickupPlanBoardService(UpdatePickupPlanBoardDto dto,
        CancellationToken cancellationToken = default)
    {
        return RetryOnDeadlockAsync(() => UpdatePickupPlanBoardCoreAsync(dto, cancellationToken), cancellationToken);
    }

    // 교착은 RetryOnDeadlockAsync가 다시 실행하므로 여기서는 500으로 바꾸지 않고 올려보낸다
    private async Task<ResponseModel<PickupPlanDto>> UpdatePickupPlanBoardCoreAsync(UpdatePickupPlanBoardDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            // 1) 인증 확인 — 로그인 사용자(USER_ID)는 HttpContext의 클레임 sub에서 꺼낸다
            if (!int.TryParse(_httpContextAccessor.HttpContext?.User.FindFirstValue("sub"), out var userId))
                return new ResponseModel<PickupPlanDto> { message = "잘못된 요청입니다.", data = null, code = 401 };

            // 2) 입력 검증 — 필수값, eventDt는 서버 현재 시각 기준 -10분 ~ +1분 안이어야 한다(경로 시작과 같다)
            if (dto?.pickupPlanSeq is null || dto.clientEventId is null || dto.eventDt is null)
                return new ResponseModel<PickupPlanDto> { message = "잘못된 요청입니다.", data = null, code = 400 };

            var now = DateTime.Now;
            var eventDt = dto.eventDt.Value;
            if (eventDt < now.AddMinutes(-StartEventPastLimitMin) || eventDt > now.AddMinutes(StartEventFutureLimitMin))
                return new ResponseModel<PickupPlanDto> { message = "잘못된 요청입니다.", data = null, code = 400 };

            var pickupPlanId = dto.pickupPlanSeq.Value;
            var clientEventId = dto.clientEventId.Value;

            // 3) 잠금 없이 소유·만료를 먼저 본다. 없거나 끝난 계획이면 같은 요청의 재전송인지 보고, 아니면 404
            var plan = await _pickupRepository.GetPlanByIdAsync(pickupPlanId, null, cancellationToken).ConfigureAwait(false);
            if (plan is null || plan.UserId is null || ClosedPlanStatuses.Contains(plan.Status))
                return await TryReplayEndedAsync(plan, pickupPlanId, clientEventId, BoardEventTypes, cancellationToken)
                           .ConfigureAwait(false)
                       ?? new ResponseModel<PickupPlanDto> { message = "잘못된 요청입니다.", data = null, code = 404 };

            if (plan.UserId != userId)
                return new ResponseModel<PickupPlanDto> { message = "잘못된 요청입니다.", data = null, code = 404 };

            // 같은 요청의 재전송은 만료 409보다 먼저 본다(네 API가 같은 순서다). BOARDED 이벤트는 계획을 끝내는 트랜잭션에서만 들어가므로
            // 보통은 위 끝난 계획 분기에서 걸린다
            var replay = await TryReplayEndedAsync(plan, pickupPlanId, clientEventId, BoardEventTypes, cancellationToken)
                .ConfigureAwait(false);
            if (replay is not null)
                return replay;

            // 진행 중인 모든 상태(PLANNED·STARTED·WAITING·STOPPED·LOOPING)에서 받는다. 정차가 늦게 오거나 빠져도 탑승은 기록한다
            if (plan.ExpireDt <= now)
                return PlanConflict(ReasonPlanClosed);

            // 4) 트랜잭션 — 예외로 빠져나가면 await using이 트랜잭션을 롤백한다
            var transaction = await _context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await using var transactionScope = transaction.ConfigureAwait(false);
            var dbTransaction = transaction.GetDbTransaction();

            // 4-1) 계획 행을 PK로 잠그는 것이 첫 문장이다(재배정이 없어 AREA는 잠그지 않는다). 잠근 값으로 다시 판정한다
            var locked = await _pickupRepository.LockPickupPlanByIdAsync(pickupPlanId, dbTransaction, cancellationToken)
                .ConfigureAwait(false);
            // 기준 시각은 잠금을 얻은 뒤에 잡는다
            var thisTime = DateTime.Now;
            if (locked is null)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return new ResponseModel<PickupPlanDto> { message = "잘못된 요청입니다.", data = null, code = 404 };
            }

            // 잠금을 기다리는 사이 계획이 끝났으면(탑승·취소·만료) 같은 요청의 재전송인지 본다
            if (locked.UserId is null || ClosedPlanStatuses.Contains(locked.Status))
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                var ended = await _pickupRepository.GetPlanByIdAsync(pickupPlanId, null, cancellationToken).ConfigureAwait(false);
                return await TryReplayEndedAsync(ended, pickupPlanId, clientEventId, BoardEventTypes, cancellationToken)
                           .ConfigureAwait(false)
                       ?? new ResponseModel<PickupPlanDto> { message = "잘못된 요청입니다.", data = null, code = 404 };
            }

            if (locked.UserId != userId)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return new ResponseModel<PickupPlanDto> { message = "잘못된 요청입니다.", data = null, code = 404 };
            }

            // 4-2) 같은 clientEventId의 탑승 이벤트가 이미 있으면 같은 요청을 다시 부른 것이다(만료 409보다 먼저 본다)
            var sameEventType = await _pickupRepository.GetPlanEventTypeAsync(pickupPlanId, clientEventId, dbTransaction,
                cancellationToken).ConfigureAwait(false);
            if (sameEventType == EventBoarded)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return PlanOk(new PickupPlanDto { pickupPlanSeq = pickupPlanId, status = locked.Status, reassigned = false });
            }

            if (locked.ExpireDt <= thisTime)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return PlanConflict(ReasonPlanClosed);
            }

            // 다른 유형의 이벤트가 그 번호를 쓰고 있으면 잘못된 요청이다
            if (sameEventType is not null)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return new ResponseModel<PickupPlanDto> { message = "잘못된 요청입니다.", data = null, code = 400 };
            }

            // 4-3) BOARD_DT는 익명화 UPDATE보다 먼저 쓴다. 이어서 계획 PK → TIMETABLE_DAY 순서로 끝내고 익명화한다.
            //      BOARDED 이벤트에는 요청의 clientEventId와 eventDt가 들어간다
            await _pickupRepository.SetBoardDtAsync(pickupPlanId, eventDt, dbTransaction, cancellationToken)
                .ConfigureAwait(false);
            if (!await _pickupRepository.ClosePickupPlanAsync(pickupPlanId, PlanStatusBoarded, true, thisTime, AuditUser,
                    dbTransaction, cancellationToken, clientEventId, eventDt).ConfigureAwait(false))
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return new ResponseModel<PickupPlanDto> { message = "서버에서 요청을 처리하지 못하였습니다.", data = null, code = 500 };
            }

            await transaction.CommitAsync(CancellationToken.None).ConfigureAwait(false);

            // 끝난 계획은 개인정보 칸이 비어 있어 계획 ID와 상태만 돌려준다
            return PlanOk(new PickupPlanDto { pickupPlanSeq = pickupPlanId, status = PlanStatusBoarded, reassigned = false });
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

    // 진행 중 계획에 같은 정차 요청이 다시 왔을 때의 응답. 지금 계획과 처음 이벤트(유형·DETAIL)로 만든 판정을 돌려준다
    private async Task<ResponseModel<PickupPlanDto>> ReplayLiveStopAsync(int pickupPlanId, string eventType,
        string? detail, CancellationToken cancellationToken)
    {
        var replayed = await BuildPlanDtoAsync(pickupPlanId, false, null, cancellationToken).ConfigureAwait(false);
        replayed.stopCheck = StopCheckFromEvent(eventType, detail);
        return PlanOk(replayed);
    }

    // 끝난 계획의 재요청(설계 4.1). 계획이 없거나 끝났을 때 부른다(탑승은 진행 중 계획의 재전송 확인에도 쓴다). 같은 계획·같은 clientEventId의 이벤트가 이 API의 유형(eventTypes)이면
    // 처음 요청이 반영된 것으로 보고 200 최소 응답(pickupPlanSeq, 지금 상태, 정차면 stopCheck)을 돌려준다. 아니면 null(호출자가 404)
    private async Task<ResponseModel<PickupPlanDto>?> TryReplayEndedAsync(LivePlanRow? plan, int pickupPlanId,
        Guid clientEventId, string[] eventTypes, CancellationToken cancellationToken)
    {
        if (plan is null)
            return null;

        var sameEvent = await _pickupRepository.GetPlanEventByClientIdAsync(pickupPlanId, clientEventId, cancellationToken)
            .ConfigureAwait(false);
        if (sameEvent is null || !eventTypes.Contains(sameEvent.EventType))
            return null;

        return PlanOk(new PickupPlanDto
        {
            pickupPlanSeq = plan.PickupPlanId,
            status = plan.Status,
            reassigned = false,
            stopCheck = StopEventTypes.Contains(sameEvent.EventType)
                ? StopCheckFromEvent(sameEvent.EventType, sameEvent.Detail)
                : null
        });
    }

    // 재요청 응답용 정차 판정. 거리와 단속 판정은 저장하지 않으므로 이벤트 유형과 DETAIL로 다시 만든다.
    // STOPPED → 배정 장소에 섰다(장소 없는 계획도 true로 나오는 한계는 받아들인다), WRONG_STOP → DETAIL의 구역 유형이 있으면 RESTRICTED
    private static PickupStopCheckDto StopCheckFromEvent(string eventType, string? detail)
    {
        if (eventType != EventWrongStop)
            return new PickupStopCheckDto
            {
                atSpot = true,
                distanceM = null,
                enforcement = EnforcementUnknown,
                zoneTypes = new List<string>()
            };

        var zoneTypes = new List<string>();
        if (detail is not null)
        {
            using var document = JsonDocument.Parse(detail);
            if (document.RootElement.TryGetProperty("zoneTypes", out var types) && types.ValueKind == JsonValueKind.Array)
                zoneTypes.AddRange(types.EnumerateArray().Select(t => t.GetString()).OfType<string>());
        }

        return new PickupStopCheckDto
        {
            atSpot = false,
            distanceM = null,
            enforcement = zoneTypes.Count > 0 ? EnforcementRestricted : EnforcementUnknown,
            zoneTypes = zoneTypes
        };
    }

    // 정차를 받을 수 있는지(설계 4.2). 남의 계획은 404, 만료 시각이 지났으면 409 PLAN_CLOSED, 한 바퀴 중이면 409 INVALID_STATUS,
    // PLANNED·STARTED·WAITING·STOPPED가 아니면 404. 받을 수 있으면 null. 끝난 계획은 호출자가 먼저 걸렀다
    private static ResponseModel<PickupPlanDto>? CheckPlanStoppable(int? ownerId, string status, DateTime expireDt,
        int userId, DateTime now)
    {
        if (ownerId != userId)
            return new ResponseModel<PickupPlanDto> { message = "잘못된 요청입니다.", data = null, code = 404 };

        if (expireDt <= now)
            return PlanConflict(ReasonPlanClosed);

        if (status == PlanStatusLooping)
            return PlanConflict(ReasonInvalidStatus);

        if (!StoppableStatuses.Contains(status))
            return new ResponseModel<PickupPlanDto> { message = "잘못된 요청입니다.", data = null, code = 404 };

        return null;
    }

    // WRONG_STOP 이벤트 DETAIL(설계 7절). 정차 좌표·거리는 넣지 않는다. 판정할 수 없으면 zoneTypes가 null
    private sealed record WrongStopDetail(List<string>? zoneTypes);
}
