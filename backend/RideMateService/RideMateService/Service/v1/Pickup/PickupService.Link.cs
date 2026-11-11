using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore.Storage;
using RideMateService.Comm;
using RideMateService.Comm.Dto.v1.Pickup.Request;
using RideMateService.Comm.Dto.v1.Pickup.Response;
using RideMateService.Commons.DBModels;

namespace RideMateService.Service.v1.Pickup;

public partial class PickupService
{
    private const string EventLinkSent = "LINK_SENT";
    private const string SendChannelSms = "SMS";
    private const string AuditUser = "PARENT";
    private const string ReasonPlanClosed = "PLAN_CLOSED";
    private const string ReasonNoSpot = "NO_SPOT";
    private const string ReasonNoChildPhone = "NO_CHILD_PHONE";
    private const string ReasonAlreadySent = "ALREADY_SENT";
    private const string ReasonInProgress = "IN_PROGRESS";
    private const string ReasonLimitReached = "LIMIT_REACHED";
    private const int EtaPastLimitMin = 10;
    private const int EtaFutureLimitHours = 3;
    private static readonly string[] ClosedPlanStatuses = { "BOARDED", "EXPIRED", "CANCELED" };

    /// <summary>
    /// 픽업 링크 문자 발송(최초 발송과 실패 후 다시 보내기)
    /// </summary>
    public async Task<ResponseModel<PickupLinkSendDto>> AddPickupLinkService(AddPickupLinkDto dto,
        CancellationToken cancellationToken = default)
    {
        // 입력 검증 — etaDt는 서버 현재 시각 기준 -10분 ~ +3시간 안이어야 한다
        if (dto?.pickupPlanSeq is null || dto.etaDt is null || dto.clientEventId is null)
            return new ResponseModel<PickupLinkSendDto> { message = "잘못된 요청입니다.", data = null, code = 400 };

        var now = DateTime.Now;
        if (dto.etaDt.Value < now.AddMinutes(-EtaPastLimitMin) || dto.etaDt.Value > now.AddHours(EtaFutureLimitHours))
            return new ResponseModel<PickupLinkSendDto> { message = "잘못된 요청입니다.", data = null, code = 400 };

        return await SendPickupMessageAsync(dto.pickupPlanSeq.Value, dto.clientEventId.Value, EventLinkSent,
            dto.etaDt.Value, cancellationToken).ConfigureAwait(false);
    }

    // 링크·다시 알림 문자 발송의 공통 흐름(설계 4.1·4.2 처리 1~8). eventType은 LINK_SENT·REMIND_SENT, etaDt는 LINK_SENT 문구에만 쓴다.
    // 진행 중 이벤트를 먼저 커밋하고 잠금 밖에서 보낸 뒤, 계획 행을 다시 잠그고 결과를 채운다
    private async Task<ResponseModel<PickupLinkSendDto>> SendPickupMessageAsync(int pickupPlanId, Guid clientEventId,
        string eventType, DateTime? etaDt, CancellationToken cancellationToken)
    {
        try
        {
            // 인증 확인 — 로그인 사용자(USER_ID)는 HttpContext의 클레임 sub에서 꺼낸다
            if (!int.TryParse(_httpContextAccessor.HttpContext?.User.FindFirstValue("sub"), out var userId))
                return new ResponseModel<PickupLinkSendDto> { message = "잘못된 요청입니다.", data = null, code = 401 };

            DateTime thisTime = DateTime.Now;
            int pickupEventId;
            string phoneDigits;
            string receiverLast4;
            string message;

            // 트랜잭션 1 — 예외로 빠져나가면 await using이 트랜잭션을 롤백한다
            {
                var transaction = await _context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
                await using var transactionScope = transaction.ConfigureAwait(false);

                // 1) 계획 행을 첫 문장으로 잠그고 공통 판정을 한다. 끝난 계획은 USER_ID가 NULL이라 404다
                var plan = await _pickupRepository.LockPickupPlanAsync(pickupPlanId, transaction.GetDbTransaction(),
                    cancellationToken).ConfigureAwait(false);
                if (plan is null || plan.UserId != userId)
                {
                    await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    return new ResponseModel<PickupLinkSendDto> { message = "잘못된 요청입니다.", data = null, code = 404 };
                }

                if (ClosedPlanStatuses.Contains(plan.Status) || plan.ExpireDt <= thisTime)
                {
                    await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    return LinkConflict(ReasonPlanClosed);
                }

                if (plan.PickupSpotId is not { } pickupSpotId)
                {
                    await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    return LinkConflict(ReasonNoSpot);
                }

                if (plan.ChildId is not { } childId)
                {
                    await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    return LinkConflict(ReasonNoChildPhone);
                }

                var target = await _pickupRepository.GetLinkTargetAsync(userId, childId, pickupSpotId,
                    transaction.GetDbTransaction(), cancellationToken).ConfigureAwait(false);
                if (target is null)
                {
                    await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    return new ResponseModel<PickupLinkSendDto> { message = "잘못된 요청입니다.", data = null, code = 404 };
                }

                if (target.SpotName is null)
                {
                    await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    return LinkConflict(ReasonNoSpot);
                }

                // 아이가 삭제됐거나 번호가 비었거나 복호화할 수 없으면 번호 없음이다. 로그에는 계획 ID만 남긴다
                string? decrypted = null;
                if (target.ChildPhoneEnc is { Length: > 0 })
                {
                    try
                    {
                        decrypted = _phoneCryptoService.Decrypt(target.ChildPhoneEnc);
                    }
                    catch (CryptographicException)
                    {
                        _logService.LogMessage($"pickup message child phone decrypt failed: plan={pickupPlanId}");
                    }
                }

                if (decrypted is null)
                {
                    await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    return LinkConflict(ReasonNoChildPhone);
                }

                phoneDigits = decrypted;
                receiverLast4 = phoneDigits[^4..];

                // 2) 같은 clientEventId가 이미 있으면 같은 요청을 다시 부른 것이다. 보내지 않고 그 결과를 돌려준다
                var events = await _pickupRepository.GetLinkEventsAsync(pickupPlanId, transaction.GetDbTransaction(),
                    cancellationToken).ConfigureAwait(false);
                var summary = PickupLinkSendState.Summarize(events, eventType, thisTime,
                    _linkOptions.InFlightTimeoutSec, _linkOptions.MaxAttempts);

                var same = events.FirstOrDefault(e => Guid.TryParse(e.ClientEventId, out var id) && id == clientEventId);
                if (same is not null)
                {
                    await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    if (same.EventType != eventType)
                        return new ResponseModel<PickupLinkSendDto> { message = "잘못된 요청입니다.", data = null, code = 400 };

                    // 결과가 없고 시간이 지나지 않았으면 원래 요청이 아직 보내는 중이다. 시간이 지났으면 실패로 본다
                    var success = PickupLinkSendState.ParseDetail(same.Detail)?.success;
                    if (success is null && same.CreateDt >= thisTime.AddSeconds(-_linkOptions.InFlightTimeoutSec))
                        return LinkConflict(ReasonInProgress, summary.RemainingAttempts, receiverLast4);

                    return LinkSendResult(success == true, summary.RemainingAttempts, receiverLast4);
                }

                // 3) 이 유형의 시도 이력으로 판정한다
                if (summary.Status == PickupLinkSendState.StatusSent)
                {
                    await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    return LinkConflict(ReasonAlreadySent, 0, receiverLast4);
                }

                if (summary.HasLiveInFlight)
                {
                    await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    return LinkConflict(ReasonInProgress, summary.RemainingAttempts, receiverLast4);
                }

                if (summary.Attempts >= _linkOptions.MaxAttempts)
                {
                    await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    return LinkConflict(ReasonLimitReached, 0, receiverLast4);
                }

                // 4) 세션이 없으면 만든다. 있으면 같은 토큰을 다시 계산해 같은 링크를 보낸다
                var session = await _pickupRepository.GetPickupSessionAsync(pickupPlanId, cancellationToken)
                    .ConfigureAwait(false);
                string token;
                if (session is null)
                {
                    var nonce = PickupLinkToken.NewNonce();
                    token = PickupLinkToken.Create(_hashService, pickupPlanId, nonce);
                    session = new PickupSession
                    {
                        PickupPlanId = pickupPlanId,
                        TokenNonce = nonce,
                        TokenHash = PickupLinkToken.HashToken(_hashService, token),
                        SendChannel = SendChannelSms,
                        CloseDt = null,
                        CreateDt = thisTime,
                        CreateUser = AuditUser,
                        UpdateDt = thisTime,
                        UpdateUser = AuditUser,
                        DeleteYn = false
                    };
                    if (!await _pickupRepository.AddPickupSessionAsync(session, cancellationToken).ConfigureAwait(false))
                    {
                        await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                        return new ResponseModel<PickupLinkSendDto> { message = "서버에서 요청을 처리하지 못하였습니다.", data = null, code = 500 };
                    }
                }
                else
                {
                    token = PickupLinkToken.Create(_hashService, pickupPlanId, session.TokenNonce);
                }

                var url = PickupLinkToken.BuildUrl(_linkOptions.BaseUrl, token);
                message = eventType == EventLinkSent
                    ? PickupLinkMessage.Link(target.UserName, etaDt!.Value, target.SpotName, url)
                    : PickupLinkMessage.Remind(target.UserName, target.SpotName, url);

                // 5) 진행 중 이벤트를 넣고 커밋한다. 같은 계획에 동시에 온 다른 요청은 이 행을 보고 IN_PROGRESS가 된다
                var pickupEvent = new PickupEvent
                {
                    PickupPlanId = pickupPlanId,
                    EventType = eventType,
                    EventDt = thisTime,
                    ClientEventId = clientEventId,
                    Detail = PickupLinkSendState.ToJson(new SendDetail(SendChannelSms, null, null)),
                    CreateDt = thisTime,
                    CreateUser = AuditUser,
                    UpdateDt = thisTime,
                    UpdateUser = AuditUser,
                    DeleteYn = false
                };
                if (!await _pickupRepository.AddPickupEventAsync(pickupEvent, cancellationToken).ConfigureAwait(false))
                {
                    await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    return new ResponseModel<PickupLinkSendDto> { message = "서버에서 요청을 처리하지 못하였습니다.", data = null, code = 500 };
                }

                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                pickupEventId = pickupEvent.PickupEventId;

                // 트랜잭션 2가 추적 중인 옛 인스턴스 대신 DB의 현재 행(그사이 익명화된 DELETE_YN 등)을 읽게 비운다
                _context.ChangeTracker.Clear();
            }

            // 6) 잠금 밖에서 보낸다. 요청이 끊겨도 발송과 결과 기록은 끝까지 한다
            var result = await _smsSender.SendAsync(phoneDigits, message, CancellationToken.None).ConfigureAwait(false);
            if (!result.Success)
                _logService.LogMessage($"pickup message send failed: plan={pickupPlanId}, type={eventType}, code={result.ResultCode}");

            // 7) 트랜잭션 2 — 계획 행을 다시 잠그고 진행 중 이벤트에 결과를 채운다. 그사이 계획이 끝났어도 기록한다
            SendSummary recorded;
            {
                var transaction = await _context.Database.BeginTransactionAsync(CancellationToken.None).ConfigureAwait(false);
                await using var transactionScope = transaction.ConfigureAwait(false);

                await _pickupRepository.LockPickupPlanAsync(pickupPlanId, transaction.GetDbTransaction(),
                    CancellationToken.None).ConfigureAwait(false);

                var pickupEvent = await _pickupRepository.GetPickupEventAsync(pickupEventId, CancellationToken.None)
                    .ConfigureAwait(false);
                if (pickupEvent is null)
                {
                    await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                    return new ResponseModel<PickupLinkSendDto> { message = "서버에서 요청을 처리하지 못하였습니다.", data = null, code = 500 };
                }

                pickupEvent.Detail = PickupLinkSendState.ToJson(new SendDetail(SendChannelSms, result.Success, result.ResultCode));
                pickupEvent.UpdateDt = DateTime.Now;
                pickupEvent.UpdateUser = AuditUser;
                if (!await _pickupRepository.UpdatePickupEventAsync(pickupEvent, CancellationToken.None).ConfigureAwait(false))
                {
                    await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                    return new ResponseModel<PickupLinkSendDto> { message = "서버에서 요청을 처리하지 못하였습니다.", data = null, code = 500 };
                }

                var events = await _pickupRepository.GetLinkEventsAsync(pickupPlanId, transaction.GetDbTransaction(),
                    CancellationToken.None).ConfigureAwait(false);
                recorded = PickupLinkSendState.Summarize(events, eventType, DateTime.Now,
                    _linkOptions.InFlightTimeoutSec, _linkOptions.MaxAttempts);

                await transaction.CommitAsync(CancellationToken.None).ConfigureAwait(false);
            }

            // 8) 응답 — 사업자가 접수했으면 200, 거절·시간 초과면 101
            return LinkSendResult(result.Success, recorded.RemainingAttempts, receiverLast4);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested) // 요청 취소는 그대로 올려보낸다
        {
            _logService.LogMessage(ex.ToString());
#if DEBUG
            _logService.ConsoleWarning(ex.ToString());
#endif
            return new ResponseModel<PickupLinkSendDto> { message = "서버에서 요청을 처리하지 못하였습니다.", data = null, code = 500 };
        }
    }

    private static ResponseModel<PickupLinkSendDto> LinkConflict(string reason, int remainingAttempts = 0,
        string? receiverLast4 = null)
    {
        return new ResponseModel<PickupLinkSendDto>
        {
            message = "잘못된 요청입니다.",
            data = new PickupLinkSendDto
            {
                reason = reason,
                sent = false,
                remainingAttempts = remainingAttempts,
                receiverLast4 = receiverLast4
            },
            code = 409
        };
    }

    private static ResponseModel<PickupLinkSendDto> LinkSendResult(bool sent, int remainingAttempts, string receiverLast4)
    {
        return new ResponseModel<PickupLinkSendDto>
        {
            message = "요청이 정상 처리되었습니다.",
            data = new PickupLinkSendDto
            {
                reason = null,
                sent = sent,
                remainingAttempts = remainingAttempts,
                receiverLast4 = receiverLast4
            },
            code = sent ? 200 : 101
        };
    }
}
