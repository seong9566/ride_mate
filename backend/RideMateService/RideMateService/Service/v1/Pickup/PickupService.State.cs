using System.Security.Claims;
using System.Security.Cryptography;
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
    private const string PlanStatusCanceled = "CANCELED";
    private const string EventCanceled = "CANCELED";
    private const string CancelNoticeNone = "NONE";
    private const string CancelNoticeSent = "SENT";
    private const string CancelNoticeFailed = "FAILED";
    private static readonly string[] BoardEventTypes = { EventBoarded };
    private static readonly string[] CancelEventTypes = { EventCanceled };
    // 도착했는지 알 수 없는 발송 실패 결과 코드(SolapiSmsSender). 그 밖의 실패는 사업자가 거절한 것이다
    private static readonly string[] UnknownDeliveryResultCodes = { "TIMEOUT", "ERROR" };
    private static readonly string[] StoppableStatuses = { "PLANNED", "STARTED", "WAITING", "STOPPED" };
    private const string PlanStatusWaiting = "WAITING";
    private const string EventWaitStarted = "WAIT_STARTED";
    private const string ReasonInvalidParkingLot = "INVALID_PARKING_LOT";
    private static readonly string[] WaitEventTypes = { EventWaitStarted };

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
            SignalPlanChanged(pickupPlanId);

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

            // 4-3) 계획 PK → TIMETABLE_DAY 순서로 끝내고 익명화한다. BOARD_DT는 그 끝내기 UPDATE 안에서 eventDt로 함께 쓴다.
            //      BOARDED 이벤트에는 요청의 clientEventId와 eventDt가 들어간다
            if (!await _pickupRepository.ClosePickupPlanAsync(pickupPlanId, PlanStatusBoarded, true, thisTime, AuditUser,
                    dbTransaction, cancellationToken, clientEventId, eventDt, boardDt: eventDt).ConfigureAwait(false))
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return new ResponseModel<PickupPlanDto> { message = "서버에서 요청을 처리하지 못하였습니다.", data = null, code = 500 };
            }

            await transaction.CommitAsync(CancellationToken.None).ConfigureAwait(false);
            SignalPlanEnded(pickupPlanId);

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

    /// <summary>
    /// 부모 취소(설계 4.4). 계획을 CANCELED로 끝내고(익명화, 링크 종료, LAST_PICKUP_DATE는 그대로) 커밋한 뒤,
    /// 아이가 링크를 받았을 수 있으면 잠금 밖에서 취소 문자를 보낸다
    /// </summary>
    public Task<ResponseModel<PickupPlanDto>> DeletePickupPlanService(DeletePickupPlanDto dto,
        CancellationToken cancellationToken = default)
    {
        return RetryOnDeadlockAsync(() => DeletePickupPlanCoreAsync(dto, cancellationToken), cancellationToken);
    }

    // 교착은 RetryOnDeadlockAsync가 다시 실행하므로 여기서는 500으로 바꾸지 않고 올려보낸다
    private async Task<ResponseModel<PickupPlanDto>> DeletePickupPlanCoreAsync(DeletePickupPlanDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            // 1) 인증 확인 — 로그인 사용자(USER_ID)는 HttpContext의 클레임 sub에서 꺼낸다
            if (!int.TryParse(_httpContextAccessor.HttpContext?.User.FindFirstValue("sub"), out var userId))
                return new ResponseModel<PickupPlanDto> { message = "잘못된 요청입니다.", data = null, code = 401 };

            // 2) 입력 검증 — 필수값
            if (dto?.pickupPlanSeq is null || dto.clientEventId is null)
                return new ResponseModel<PickupPlanDto> { message = "잘못된 요청입니다.", data = null, code = 400 };

            var now = DateTime.Now;
            var pickupPlanId = dto.pickupPlanSeq.Value;
            var clientEventId = dto.clientEventId.Value;

            // 3) 잠금 없이 소유·만료를 먼저 본다. 없거나 끝난 계획이면 같은 요청의 재전송인지 보고, 아니면 404
            var plan = await _pickupRepository.GetPlanByIdAsync(pickupPlanId, null, cancellationToken).ConfigureAwait(false);
            if (plan is null || plan.UserId is null || ClosedPlanStatuses.Contains(plan.Status))
                return await TryReplayEndedAsync(plan, pickupPlanId, clientEventId, CancelEventTypes, cancellationToken)
                           .ConfigureAwait(false)
                       ?? new ResponseModel<PickupPlanDto> { message = "잘못된 요청입니다.", data = null, code = 404 };

            if (plan.UserId != userId)
                return new ResponseModel<PickupPlanDto> { message = "잘못된 요청입니다.", data = null, code = 404 };

            // 같은 요청의 재전송은 만료 409보다 먼저 본다(네 API가 같은 순서다). CANCELED 이벤트는 계획을 끝내는 트랜잭션에서만 들어가므로
            // 보통은 위 끝난 계획 분기에서 걸린다
            var replay = await TryReplayEndedAsync(plan, pickupPlanId, clientEventId, CancelEventTypes, cancellationToken)
                .ConfigureAwait(false);
            if (replay is not null)
                return replay;

            // 진행 중인 모든 상태(PLANNED·STARTED·WAITING·STOPPED·LOOPING)에서 받는다
            if (plan.ExpireDt <= now)
                return PlanConflict(ReasonPlanClosed);

            string? noticeUserName = null;
            string? noticePhone = null;

            // 4) 트랜잭션 — 예외로 빠져나가면 await using이 트랜잭션을 롤백한다. 문자는 커밋한 뒤 이 블록 밖에서 보낸다
            {
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
                    return await TryReplayEndedAsync(ended, pickupPlanId, clientEventId, CancelEventTypes, cancellationToken)
                               .ConfigureAwait(false)
                           ?? new ResponseModel<PickupPlanDto> { message = "잘못된 요청입니다.", data = null, code = 404 };
                }

                if (locked.UserId != userId)
                {
                    await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    return new ResponseModel<PickupPlanDto> { message = "잘못된 요청입니다.", data = null, code = 404 };
                }

                // 4-2) 같은 clientEventId의 취소 이벤트가 이미 있으면 같은 요청을 다시 부른 것이다(만료 409보다 먼저 본다). 문자는 보내지 않는다
                var sameEventType = await _pickupRepository.GetPlanEventTypeAsync(pickupPlanId, clientEventId, dbTransaction,
                    cancellationToken).ConfigureAwait(false);
                if (sameEventType == EventCanceled)
                {
                    await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    return PlanOk(new PickupPlanDto
                    {
                        pickupPlanSeq = pickupPlanId,
                        status = locked.Status,
                        reassigned = false,
                        cancelNotice = CancelNoticeNone
                    });
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

                // 4-3) 아이가 링크를 받았을 수 있으면 취소 문자 대상이다. 익명화하면 참조가 사라지므로 번호와 운전자 이름(USER_INFO.USER_NAME, 취소 문자에서는 부모 이름 자리)을 지금 읽어 둔다.
                //      아이가 삭제됐거나 번호가 없거나 복호화할 수 없으면 대상이 아니다. 로그에는 계획 ID만 남긴다
                var events = await _pickupRepository.GetLinkEventsAsync(pickupPlanId, dbTransaction, cancellationToken)
                    .ConfigureAwait(false);
                if (ChildMayHaveLink(events))
                {
                    var target = await _pickupRepository.GetCancelNoticeTargetAsync(pickupPlanId, dbTransaction,
                        cancellationToken).ConfigureAwait(false);
                    if (target?.ChildPhoneEnc is { Length: > 0 } phoneEnc)
                    {
                        try
                        {
                            noticePhone = _phoneCryptoService.Decrypt(phoneEnc);
                            noticeUserName = target.UserName;
                        }
                        catch (CryptographicException)
                        {
                            _logService.LogMessage($"pickup cancel notice child phone decrypt failed: plan={pickupPlanId}");
                        }
                    }
                }

                // 4-4) 계획 PK → TIMETABLE_DAY 순서로 끝내고 익명화한다. LAST_PICKUP_DATE는 바꾸지 않아 그날 다시 만들 수 있다.
                //      CANCELED 이벤트에는 요청의 clientEventId와 서버 시각이 들어간다
                if (!await _pickupRepository.ClosePickupPlanAsync(pickupPlanId, PlanStatusCanceled, false, thisTime, AuditUser,
                        dbTransaction, cancellationToken, clientEventId, thisTime).ConfigureAwait(false))
                {
                    await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    return new ResponseModel<PickupPlanDto> { message = "서버에서 요청을 처리하지 못하였습니다.", data = null, code = 500 };
                }

                await transaction.CommitAsync(CancellationToken.None).ConfigureAwait(false);
                SignalPlanEnded(pickupPlanId);
            }

            // 5) 잠금 밖에서 취소 문자를 보낸다. 요청이 끊겨도 보낸다. 결과는 응답으로만 알리고 실패해도 다시 보내지 않는다
            var cancelNotice = CancelNoticeNone;
            if (noticePhone is not null && noticeUserName is not null)
            {
                // 취소는 이미 커밋했으므로 발송 중 예외가 나도 500이 아니라 FAILED로 답한다. 로그에는 계획 ID만 남긴다
                try
                {
                    var result = await _smsSender.SendAsync(noticePhone, PickupLinkMessage.Cancel(noticeUserName),
                        CancellationToken.None).ConfigureAwait(false);
                    cancelNotice = result.Success ? CancelNoticeSent : CancelNoticeFailed;
                    if (!result.Success)
                        _logService.LogMessage($"pickup cancel notice failed: plan={pickupPlanId}, code={result.ResultCode}");
                }
                catch (Exception)
                {
                    cancelNotice = CancelNoticeFailed;
                    _logService.LogMessage($"pickup cancel notice failed: plan={pickupPlanId}, exception");
                }
            }

            // 끝난 계획은 개인정보 칸이 비어 있어 계획 ID와 상태, 취소 문자 결과만 돌려준다
            return PlanOk(new PickupPlanDto
            {
                pickupPlanSeq = pickupPlanId,
                status = PlanStatusCanceled,
                reassigned = false,
                cancelNotice = cancelNotice
            });
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
    /// 대기 장소 도착(설계 4.5). 같은 학원가의 주차장이면 계획을 WAITING으로 바꾸고 주차장을 기록한다.
    /// 이미 WAITING이면 주차장만 바꾸고 WAIT_START_DT는 그대로 둔다
    /// </summary>
    public Task<ResponseModel<PickupPlanDto>> UpdatePickupPlanWaitService(UpdatePickupPlanWaitDto dto,
        CancellationToken cancellationToken = default)
    {
        return RetryOnDeadlockAsync(() => UpdatePickupPlanWaitCoreAsync(dto, cancellationToken), cancellationToken);
    }

    // 교착은 RetryOnDeadlockAsync가 다시 실행하므로 여기서는 500으로 바꾸지 않고 올려보낸다
    private async Task<ResponseModel<PickupPlanDto>> UpdatePickupPlanWaitCoreAsync(UpdatePickupPlanWaitDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            // 1) 인증 확인 — 로그인 사용자(USER_ID)는 HttpContext의 클레임 sub에서 꺼낸다
            if (!int.TryParse(_httpContextAccessor.HttpContext?.User.FindFirstValue("sub"), out var userId))
                return new ResponseModel<PickupPlanDto> { message = "잘못된 요청입니다.", data = null, code = 401 };

            // 2) 입력 검증 — 필수값, 주차장 ID는 1 이상, eventDt는 서버 현재 시각 기준 -10분 ~ +1분 안이어야 한다(경로 시작과 같다)
            if (dto?.pickupPlanSeq is null || dto.clientEventId is null || dto.eventDt is null
                || dto.parkingLotSeq is not > 0)
                return new ResponseModel<PickupPlanDto> { message = "잘못된 요청입니다.", data = null, code = 400 };

            var now = DateTime.Now;
            var eventDt = dto.eventDt.Value;
            if (eventDt < now.AddMinutes(-StartEventPastLimitMin) || eventDt > now.AddMinutes(StartEventFutureLimitMin))
                return new ResponseModel<PickupPlanDto> { message = "잘못된 요청입니다.", data = null, code = 400 };

            var pickupPlanId = dto.pickupPlanSeq.Value;
            var clientEventId = dto.clientEventId.Value;
            var parkingLotId = dto.parkingLotSeq.Value;

            // 3) 잠금 없이 소유·상태·만료를 먼저 본다. 없거나 끝난 계획이면 같은 요청의 재전송인지 보고, 아니면 404
            var plan = await _pickupRepository.GetPlanByIdAsync(pickupPlanId, null, cancellationToken).ConfigureAwait(false);
            if (plan is null || plan.UserId is null || ClosedPlanStatuses.Contains(plan.Status))
                return await TryReplayEndedAsync(plan, pickupPlanId, clientEventId, WaitEventTypes, cancellationToken)
                           .ConfigureAwait(false)
                       ?? new ResponseModel<PickupPlanDto> { message = "잘못된 요청입니다.", data = null, code = 404 };

            if (plan.UserId != userId)
                return new ResponseModel<PickupPlanDto> { message = "잘못된 요청입니다.", data = null, code = 404 };

            // 같은 요청의 재전송은 만료·상태 409보다 먼저 본다(네 API가 같은 순서다). 지금 계획을 돌려준다
            var sameEvent = await _pickupRepository.GetPlanEventByClientIdAsync(pickupPlanId, clientEventId,
                cancellationToken).ConfigureAwait(false);
            if (sameEvent?.EventType == EventWaitStarted)
                return PlanOk(await BuildPlanDtoAsync(pickupPlanId, false, null, cancellationToken).ConfigureAwait(false));

            // PLANNED·STARTED·WAITING만 받는다. 정차·한 바퀴 중이면 409 ALREADY_STOPPED, 만료 시각이 지났으면 409 PLAN_CLOSED
            var rejected = CheckPlanLocationChangeable(plan.UserId, plan.Status, plan.ExpireDt, userId, now);
            if (rejected is not null)
                return rejected;

            // 4) 주차장은 계획과 같은 학원가의 살아 있는 것이어야 한다. 계획의 AREA_ID는 바뀌지 않으므로 잠근 뒤 다시 보지 않는다
            //    PARKING_LOT은 AVAILABLE_SPACES 갱신과 소프트 삭제로만 바뀌고 FK에는 영향이 없어, 사이에 삭제돼도 삭제 전에 쓴 것과 결과가 같다
            if (!await _pickupRepository.IsParkingLotInAreaAsync(parkingLotId, plan.AreaId, cancellationToken)
                    .ConfigureAwait(false))
                return PlanConflict(ReasonInvalidParkingLot);

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
                return await TryReplayEndedAsync(ended, pickupPlanId, clientEventId, WaitEventTypes, cancellationToken)
                           .ConfigureAwait(false)
                       ?? new ResponseModel<PickupPlanDto> { message = "잘못된 요청입니다.", data = null, code = 404 };
            }

            if (locked.UserId != userId)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return new ResponseModel<PickupPlanDto> { message = "잘못된 요청입니다.", data = null, code = 404 };
            }

            // 5-2) 같은 clientEventId의 대기 이벤트가 이미 있으면 같은 요청을 다시 부른 것이다. 만료·상태 409보다 먼저 보고 지금 계획을 돌려준다
            var sameEventType = await _pickupRepository.GetPlanEventTypeAsync(pickupPlanId, clientEventId, dbTransaction,
                cancellationToken).ConfigureAwait(false);
            if (sameEventType == EventWaitStarted)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return PlanOk(await BuildPlanDtoAsync(pickupPlanId, false, null, cancellationToken).ConfigureAwait(false));
            }

            rejected = CheckPlanLocationChangeable(locked.UserId, locked.Status, locked.ExpireDt, userId, thisTime);
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

            var entity = await _pickupRepository.GetPickupPlanEntityAsync(pickupPlanId, cancellationToken).ConfigureAwait(false);
            if (entity is null)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return new ResponseModel<PickupPlanDto> { message = "잘못된 요청입니다.", data = null, code = 404 };
            }

            // 5-3) STATUS = WAITING. WAIT_START_DT는 처음 도착한 시각이라 비어 있을 때만 채운다(다른 주차장으로 옮겨도 그대로)
            entity.Status = PlanStatusWaiting;
            entity.WaitStartDt ??= eventDt;
            entity.ParkingLotId = parkingLotId;
            entity.UpdateDt = thisTime;
            entity.UpdateUser = AuditUser;

            if (!await _pickupRepository.UpdatePickupPlanAsync(entity, cancellationToken).ConfigureAwait(false))
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return new ResponseModel<PickupPlanDto> { message = "서버에서 요청을 처리하지 못하였습니다.", data = null, code = 500 };
            }

            // 5-4) WAIT_STARTED 이벤트(DETAIL = 주차장 ID, 설계 7절)
            var waitEvent = new PickupEvent
            {
                PickupPlanId = pickupPlanId,
                EventType = EventWaitStarted,
                EventDt = eventDt,
                ClientEventId = clientEventId,
                Detail = JsonSerializer.Serialize(new WaitStartedDetail(parkingLotId)),
                CreateDt = thisTime,
                CreateUser = AuditUser,
                UpdateDt = thisTime,
                UpdateUser = AuditUser,
                DeleteYn = false
            };
            if (!await _pickupRepository.AddPickupEventAsync(waitEvent, cancellationToken).ConfigureAwait(false))
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return new ResponseModel<PickupPlanDto> { message = "서버에서 요청을 처리하지 못하였습니다.", data = null, code = 500 };
            }

            await transaction.CommitAsync(CancellationToken.None).ConfigureAwait(false);
            SignalPlanChanged(pickupPlanId);

            return PlanOk(await BuildPlanDtoAsync(pickupPlanId, false, null, cancellationToken).ConfigureAwait(false));
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

    // 취소 문자 대상인지(설계 4.4). LINK_SENT 중 하나라도 성공했거나, 결과가 없거나(진행 중), 도착했는지 모르는 실패(TIMEOUT·ERROR)면
    // 아이가 링크를 받았을 수 있다. 사업자가 거절한 실패만 있거나 링크 발송이 없으면 false
    private static bool ChildMayHaveLink(IReadOnlyList<LinkEventRow> events)
    {
        foreach (var row in events.Where(e => e.EventType == EventLinkSent))
        {
            var detail = PickupLinkSendState.ParseDetail(row.Detail);
            if (detail?.success is null or true)
                return true;

            if (detail.resultCode is not null && UnknownDeliveryResultCodes.Contains(detail.resultCode))
                return true;
        }

        return false;
    }

    // 진행 중 계획에 같은 정차 요청이 다시 왔을 때의 응답. 지금 계획과 처음 이벤트(유형·DETAIL)로 만든 판정을 돌려준다
    private async Task<ResponseModel<PickupPlanDto>> ReplayLiveStopAsync(int pickupPlanId, string eventType,
        string? detail, CancellationToken cancellationToken)
    {
        var replayed = await BuildPlanDtoAsync(pickupPlanId, false, null, cancellationToken).ConfigureAwait(false);
        replayed.stopCheck = StopCheckFromEvent(eventType, detail);
        return PlanOk(replayed);
    }

    // 끝난 계획의 재요청(설계 4.1). 계획이 없거나 끝났을 때 부른다(탑승·취소는 진행 중 계획의 재전송 확인에도 쓴다). 같은 계획·같은 clientEventId의 이벤트가 이 API의 유형(eventTypes)이면
    // 처음 요청이 반영된 것으로 보고 200 최소 응답(pickupPlanSeq, 지금 상태, 정차면 stopCheck, 취소면 cancelNotice NONE)을 돌려준다. 아니면 null(호출자가 404)
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
                : null,
            // 취소 문자는 처음 요청에서만 보낸다. 재요청은 보내지 않으므로 NONE이다
            cancelNotice = sameEvent.EventType == EventCanceled ? CancelNoticeNone : null
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

    // WAIT_STARTED 이벤트 DETAIL(설계 7절)
    private sealed record WaitStartedDetail(int parkingLotId);
}
