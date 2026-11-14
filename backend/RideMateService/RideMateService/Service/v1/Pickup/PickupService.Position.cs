using System.Security.Claims;
using RideMateService.Comm;
using RideMateService.Comm.Dto.v1.Pickup.Request;
using RideMateService.Comm.Dto.v1.Pickup.Response;

namespace RideMateService.Service.v1.Pickup;

public partial class PickupService
{
    /// <summary>
    /// 차 위치·내비 남은 시간을 보고한다. DB에 쓰지 않고 계획별 마지막 보고만 메모리에 둔다
    /// </summary>
    public async Task<ResponseModel<PickupPlanPositionDto>> UpdatePickupPlanPositionService(
        UpdatePickupPlanPositionDto dto, CancellationToken cancellationToken = default)
    {
        int? planId = null;
        try
        {
            if (!int.TryParse(_httpContextAccessor.HttpContext?.User.FindFirstValue("sub"), out var userId))
                return PositionUnauthorized();

            planId = dto.pickupPlanSeq!.Value;
            var plan = await _pickupRepository.GetPositionPlanAsync(planId.Value, cancellationToken)
                .ConfigureAwait(false);
            if (plan is null || plan.UserId != userId || ClosedPlanStatuses.Contains(plan.Status))
                return PositionNotFound();

            var now = NowToSecond();
            if (plan.ExpireDt <= now)
                return PositionConflict("PLAN_CLOSED");
            if (plan.Status == "PLANNED")
                return PositionConflict("NOT_STARTED");
            if (!_liveOptions.VehiclePositionEnabled)
                return PositionSuccess();

            _liveHub.TrySetVehiclePosition(planId.Value,
                new VehiclePosition(dto.lat!.Value, dto.lng!.Value, dto.etaSec, now), plan.StopDt);
            return PositionSuccess();
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested) // 요청 취소는 그대로 올려보낸다
        {
            _logService.LogMessage($"vehicle position failed: plan={planId?.ToString() ?? "unknown"}\n{ex}");
#if DEBUG
            _logService.ConsoleWarning($"vehicle position failed: plan={planId?.ToString() ?? "unknown"}\n{ex}");
#endif
            return PositionServerError();
        }
    }

    private static ResponseModel<PickupPlanPositionDto> PositionSuccess()
    {
        return new ResponseModel<PickupPlanPositionDto>
        {
            message = "요청이 정상 처리되었습니다.",
            data = null,
            code = 200
        };
    }

    private static ResponseModel<PickupPlanPositionDto> PositionUnauthorized()
    {
        return new ResponseModel<PickupPlanPositionDto>
        {
            message = "잘못된 요청입니다.",
            data = null,
            code = 401
        };
    }

    private static ResponseModel<PickupPlanPositionDto> PositionNotFound()
    {
        return new ResponseModel<PickupPlanPositionDto>
        {
            message = "잘못된 요청입니다.",
            data = null,
            code = 404
        };
    }

    private static ResponseModel<PickupPlanPositionDto> PositionConflict(string reason)
    {
        return new ResponseModel<PickupPlanPositionDto>
        {
            message = "잘못된 요청입니다.",
            data = new PickupPlanPositionDto { reason = reason },
            code = 409
        };
    }

    private static ResponseModel<PickupPlanPositionDto> PositionServerError()
    {
        return new ResponseModel<PickupPlanPositionDto>
        {
            message = "서버에서 요청을 처리하지 못하였습니다.",
            data = null,
            code = 500
        };
    }
}
