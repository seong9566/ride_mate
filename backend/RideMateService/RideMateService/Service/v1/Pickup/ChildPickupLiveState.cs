using Newtonsoft.Json;
using RideMateService.Comm.Dto.v1.Pickup.Response;

namespace RideMateService.Service.v1.Pickup;

/// <summary>시각·차량 위치·도착 예정을 제외한 아이 픽업 스냅샷 변경 판정</summary>
public static class ChildPickupLiveState
{
    /// <summary>장소, 상태, 출발 알림, 기타 순서로 변경 종류를 돌려준다. 변경이 없으면 null</summary>
    public static string? Compare(ChildPickupDto previous, ChildPickupDto next)
    {
        if (!Same(previous.pickup, next.pickup) || previous.slotDt != next.slotDt)
            return "PLACE_CHANGED";
        if (previous.planStatus != next.planStatus || previous.stopAtSpot != next.stopAtSpot
            || previous.linkStatus != next.linkStatus)
            return "STATUS_CHANGED";
        if (!Same(previous.leave, next.leave))
            return "LEAVE_CHANGED";
        if (previous.driverName != next.driverName || !Same(previous.car, next.car)
            || !Same(previous.academy, next.academy) || !Same(previous.walkPath, next.walkPath))
            return "OTHER";
        return null;
    }

    private static bool Same(object? previous, object? next)
    {
        return JsonConvert.SerializeObject(previous) == JsonConvert.SerializeObject(next);
    }
}
