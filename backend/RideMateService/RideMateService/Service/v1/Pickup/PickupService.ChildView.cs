using RideMateService.Comm;
using RideMateService.Comm.Dto.v1.Pickup.Response;

namespace RideMateService.Service.v1.Pickup;

public partial class PickupService
{
    private const string LinkStatusActive = "ACTIVE";
    private const string LinkStatusBoarded = "BOARDED";
    private const string LinkStatusClosed = "CLOSED";
    private const string ArrivalSourceSlot = "SLOT";

    /// <summary>
    /// 아이 픽업 안내 조회
    /// </summary>
    public async Task<ResponseModel<ChildPickupDto>> DetailChildPickupService(string? token,
        CancellationToken cancellationToken = default)
    {
        int? pickupPlanId = null;
        try
        {
            if (!PickupLinkToken.IsWellFormed(token))
                return ChildPickupNotFound();

            var session = await _pickupRepository.GetLeaveSessionAsync(
                PickupLinkToken.HashToken(_hashService, token!), cancellationToken).ConfigureAwait(false);
            if (session is null)
                return ChildPickupNotFound();

            pickupPlanId = session.PickupPlanId;
            var plan = await _pickupRepository.GetPlanByIdAsync(session.PickupPlanId, null,
                cancellationToken).ConfigureAwait(false);
            if (plan is null)
                return ChildPickupNotFound();

            var now = NowToSecond();
            var linkStatus = plan.Status == PlanStatusBoarded
                ? LinkStatusBoarded
                : ClosedPlanStatuses.Contains(plan.Status) || plan.ExpireDt <= now || session.CloseDt is not null
                    ? LinkStatusClosed
                    : LinkStatusActive;
            if (linkStatus != LinkStatusActive)
                return ChildPickupSuccess(new ChildPickupDto { linkStatus = linkStatus, serverDt = now });

            var driver = await _pickupRepository.GetChildPickupDriverAsync(plan.PickupPlanId,
                cancellationToken).ConfigureAwait(false);
            var pickup = ToPlanLocation(plan);
            List<GeoPointDto>? walkPath = null;
            if (pickup?.locationType == LocationTypeCustom)
            {
                walkPath = ParseLineString(plan.CustomWalkPathWkt);
            }
            else if (pickup?.locationType == LocationTypeSpot && plan.AcademyId is { } academyId)
            {
                var spots = await _pickupRepository.GetAcademyPickupSpotsAsync(academyId,
                    cancellationToken).ConfigureAwait(false);
                walkPath = ParseLineString(spots.FirstOrDefault(spot => spot.PickupSpotId == plan.PickupSpotId)?.WalkPathWkt);
            }

            bool? stopAtSpot = null;
            if (plan.Status == PlanStatusStopped)
                stopAtSpot = await _pickupRepository.GetLastStopEventTypeAsync(plan.PickupPlanId,
                    cancellationToken).ConfigureAwait(false) == EventStopped;

            var events = await _pickupRepository.GetLeaveEventsAsync(plan.PickupPlanId, null,
                cancellationToken).ConfigureAwait(false);
            var summary = ChildLeaveState.Summarize(events, now, _linkOptions.LeaveSoonMin);
            return ChildPickupSuccess(new ChildPickupDto
            {
                linkStatus = linkStatus,
                serverDt = now,
                driverName = driver?.UserName,
                car = driver is null ? null : new ChildPickupCarDto
                {
                    model = driver.CarModel,
                    color = driver.CarColor,
                    noLast4 = driver.CarNoLast4
                },
                academy = plan.AcademyLat is { } academyLat && plan.AcademyLng is { } academyLng
                    ? new GeoPointDto { lat = academyLat, lng = academyLng }
                    : null,
                pickup = pickup,
                walkPath = walkPath,
                planStatus = plan.Status,
                stopAtSpot = stopAtSpot,
                slotDt = plan.SlotDt,
                arrivalDt = plan.SlotDt,
                arrivalSource = plan.SlotDt is null ? null : ArrivalSourceSlot,
                vehicle = null,
                leave = new ChildPickupLeaveDto
                {
                    leaveStatus = summary.Status,
                    plannedLeaveDt = summary.PlannedLeaveDt,
                    leavingDt = summary.LeavingDt
                }
            });
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested) // 요청 취소는 그대로 올려보낸다
        {
            _logService.LogMessage($"child pickup detail failed: plan={pickupPlanId?.ToString() ?? "unknown"}\n{ex}");
#if DEBUG
            _logService.ConsoleWarning($"child pickup detail failed: plan={pickupPlanId?.ToString() ?? "unknown"}\n{ex}");
#endif
            return ChildPickupServerError();
        }
    }

    private static ResponseModel<ChildPickupDto> ChildPickupSuccess(ChildPickupDto dto)
    {
        return new ResponseModel<ChildPickupDto>
        {
            message = "요청이 정상 처리되었습니다.",
            data = dto,
            code = 200
        };
    }

    private static ResponseModel<ChildPickupDto> ChildPickupNotFound()
    {
        return new ResponseModel<ChildPickupDto> { message = "잘못된 요청입니다.", data = null, code = 404 };
    }

    private static ResponseModel<ChildPickupDto> ChildPickupServerError()
    {
        return new ResponseModel<ChildPickupDto>
        {
            message = "서버에서 요청을 처리하지 못하였습니다.",
            data = null,
            code = 500
        };
    }
}
