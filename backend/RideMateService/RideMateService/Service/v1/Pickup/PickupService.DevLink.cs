using System.Security.Claims;
using Microsoft.EntityFrameworkCore.Storage;
using RideMateService.Comm;
using RideMateService.Comm.Dto.v1.Pickup.Request;
using RideMateService.Comm.Dto.v1.Pickup.Response;
using RideMateService.Commons.DBModels;

namespace RideMateService.Service.v1.Pickup;

public partial class PickupService
{
    private const string DevLinkResultCode = "DEV_LINK";
    // 개발 전용 링크는 끝나지 않게 계획 만료 시각을 DATETIME 최대값으로 둔다
    private static readonly DateTime DevLinkExpireDt = new(9999, 12, 31, 23, 59, 59);

    /// <summary>
    /// 개발 전용 픽업 링크 발급. 문자를 보내지 않고 세션·LINK_SENT를 기록한 뒤 토큰과 링크를 돌려준다. 계획 만료 시각을 최대로 늦춘다
    /// </summary>
    public async Task<ResponseModel<DevPickupLinkDto>> AddDevPickupLinkService(AddDevPickupLinkDto dto,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // 1) 인증 확인 — 로그인 사용자(USER_ID)는 HttpContext의 클레임 sub에서 꺼낸다
            if (!int.TryParse(_httpContextAccessor.HttpContext?.User.FindFirstValue("sub"), out var userId))
                return new ResponseModel<DevPickupLinkDto> { message = "잘못된 요청입니다.", data = null, code = 401 };

            // 2) 입력 검증 — 계획 ID가 빠지면 거절한다
            if (dto?.pickupPlanSeq is not { } pickupPlanId)
                return new ResponseModel<DevPickupLinkDto> { message = "잘못된 요청입니다.", data = null, code = 400 };

            // 3) 트랜잭션 — 예외로 빠져나가면 await using이 트랜잭션을 롤백한다
            var transaction = await _context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await using var transactionScope = transaction.ConfigureAwait(false);

            // 3-1) 계획 행을 첫 문장으로 잠근다. 끝난 계획은 USER_ID가 NULL이라 404다
            var plan = await _pickupRepository.LockPickupPlanAsync(pickupPlanId, transaction.GetDbTransaction(),
                cancellationToken).ConfigureAwait(false);
            var thisTime = DateTime.Now;
            if (plan is null || plan.UserId != userId)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return new ResponseModel<DevPickupLinkDto> { message = "잘못된 요청입니다.", data = null, code = 404 };
            }

            if (ClosedPlanStatuses.Contains(plan.Status) || plan.ExpireDt <= thisTime)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return DevLinkConflict(ReasonPlanClosed, pickupPlanId);
            }

            // 아이 웹이 보여줄 만날 장소가 없으면 링크를 만들지 않는다(AddPickupLink와 같다)
            if (plan.PickupSpotId is null && plan.CustomName is null)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return DevLinkConflict(ReasonNoSpot, pickupPlanId);
            }

            // 3-2) 세션이 없으면 만들고, 있으면 같은 토큰을 다시 계산한다
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
                    return new ResponseModel<DevPickupLinkDto> { message = "서버에서 요청을 처리하지 못하였습니다.", data = null, code = 500 };
                }
            }
            else
            {
                token = PickupLinkToken.Create(_hashService, pickupPlanId, session.TokenNonce);
            }

            // 3-3) 접수된 LINK_SENT가 없으면 보낸 것으로 기록한다. 실제 발송이 아니라 결과 코드를 DEV로 남긴다
            var events = await _pickupRepository.GetLinkEventsAsync(pickupPlanId, transaction.GetDbTransaction(),
                cancellationToken).ConfigureAwait(false);
            var summary = PickupLinkSendState.Summarize(events, EventLinkSent, thisTime,
                _linkOptions.InFlightTimeoutSec, _linkOptions.MaxAttempts);
            if (summary.Status != PickupLinkSendState.StatusSent)
            {
                var sentEvent = new PickupEvent
                {
                    PickupPlanId = pickupPlanId,
                    EventType = EventLinkSent,
                    EventDt = thisTime,
                    ClientEventId = Guid.NewGuid(),
                    Detail = PickupLinkSendState.ToJson(new SendDetail(SendChannelSms, true, DevLinkResultCode)),
                    CreateDt = thisTime,
                    CreateUser = AuditUser,
                    UpdateDt = thisTime,
                    UpdateUser = AuditUser,
                    DeleteYn = false
                };
                if (!await _pickupRepository.AddPickupEventAsync(sentEvent, cancellationToken).ConfigureAwait(false))
                {
                    await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    return new ResponseModel<DevPickupLinkDto> { message = "서버에서 요청을 처리하지 못하였습니다.", data = null, code = 500 };
                }
            }

            // 3-4) 계획 만료 시각을 최대로 늦춘다. 재배정·장소 바꾸기·하원 시각 변경은 만료 시각을 다시 계산한다
            var entity = await _pickupRepository.GetPickupPlanEntityAsync(pickupPlanId, cancellationToken)
                .ConfigureAwait(false);
            if (entity is null)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return new ResponseModel<DevPickupLinkDto> { message = "서버에서 요청을 처리하지 못하였습니다.", data = null, code = 500 };
            }

            entity.ExpireDt = DevLinkExpireDt;
            entity.UpdateDt = thisTime;
            entity.UpdateUser = AuditUser;
            if (!await _pickupRepository.UpdatePickupPlanAsync(entity, cancellationToken).ConfigureAwait(false))
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return new ResponseModel<DevPickupLinkDto> { message = "서버에서 요청을 처리하지 못하였습니다.", data = null, code = 500 };
            }

            await transaction.CommitAsync(CancellationToken.None).ConfigureAwait(false);
            SignalPlanChanged(pickupPlanId);

            // 4) 응답
            return new ResponseModel<DevPickupLinkDto>
            {
                message = "요청이 정상 처리되었습니다.",
                data = new DevPickupLinkDto
                {
                    pickupPlanSeq = pickupPlanId,
                    token = token,
                    url = PickupLinkToken.BuildUrl(_linkOptions.BaseUrl, token),
                    expireDt = DevLinkExpireDt
                },
                code = 200
            };
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested) // 요청 취소는 그대로 올려보낸다
        {
            _logService.LogMessage(ex.ToString());
#if DEBUG
            _logService.ConsoleWarning(ex.ToString());
#endif
            return new ResponseModel<DevPickupLinkDto> { message = "서버에서 요청을 처리하지 못하였습니다.", data = null, code = 500 };
        }
    }

    private static ResponseModel<DevPickupLinkDto> DevLinkConflict(string reason, int pickupPlanId)
    {
        return new ResponseModel<DevPickupLinkDto>
        {
            message = "잘못된 요청입니다.",
            data = new DevPickupLinkDto { reason = reason, pickupPlanSeq = pickupPlanId },
            code = 409
        };
    }
}
