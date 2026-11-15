using RideMateService.Comm.Dto.v1.Pickup.Response;

namespace RideMateService.Service.v1.Pickup;

public partial class PickupService
{
    private async Task ApplyCongestionAsync(DepartureComputation computation, CancellationToken ct)
    {
        var result = computation.Result;
        if (!_options.Congestion.Enabled
            || result?.departureDt is null
            || result.driveRoute?.source != DriveRouteSourceRoute
            || computation.IsHoliday
            || computation.Target?.AreaId is not { } areaId)
            return;

        var tPlan = computation.TargetArrival;
        if (tPlan is null || computation.Now.Date != tPlan.Value.Date)
            return;

        var nowSlot = CongestionCalculator.SlotMin(computation.Now);
        var planSlot = CongestionCalculator.SlotMin(tPlan.Value);
        if (nowSlot >= planSlot)
            return;

        try
        {
            var rows = await _pickupRepository.GetTrafficProfileAsync(areaId,
                DepartureGuideCalculator.DayOfWeek(DateOnly.FromDateTime(tPlan.Value)),
                new[] { planSlot, nowSlot }, ct).ConfigureAwait(false);
            var speeds = CongestionCalculator.PickSpeeds(rows, planSlot, nowSlot,
                _options.Congestion.MinSampleDays);
            if (speeds is null)
                return;

            var areaCode = rows.FirstOrDefault()?.AreaCode;
            if (areaCode is null || !_options.Congestion.EntryDistanceM.TryGetValue(areaCode, out var entryDistanceM))
                return;

            var minutes = CongestionCalculator.Minutes(entryDistanceM, speeds.Value.PlanKmh,
                speeds.Value.NowKmh, _options.Congestion.MaxCongestionMin);
            if (minutes == 0)
                return;

            result.departureDt = result.departureDt.Value - TimeSpan.FromMinutes(minutes);
            result.slackMin = DepartureGuideCalculator.SlackMinutes(result.departureDt.Value, computation.Now);
            result.reasons.Add(new DepartureGuideReasonDto
            {
                code = ReasonRoadCongestion,
                congestionMin = minutes,
                areaCode = areaCode
            });
        }
        catch (Exception ex) when (!ct.IsCancellationRequested) // 요청 취소는 그대로 올려보낸다
        {
            _logService.LogMessage(ex.ToString());
#if DEBUG
            _logService.ConsoleWarning(ex.ToString());
#endif
        }
    }
}
