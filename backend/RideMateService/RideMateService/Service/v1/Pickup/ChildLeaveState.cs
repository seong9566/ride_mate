using System.Globalization;
using System.Text.Json;
using RideMateService.Repository.v1.Pickup;

namespace RideMateService.Service.v1.Pickup;

/// <summary>
/// 아이 출발 상태 요약
/// </summary>
/// <param name="Status">NONE, SCHEDULED, SOON, LEAVING</param>
/// <param name="PlannedLeaveDt">예정 출발 시각. SCHEDULED·SOON일 때만 값이 있다</param>
/// <param name="LeavingDt">처음 "지금 나가요"를 누른 시각</param>
public sealed record ChildLeaveSummary(string Status, DateTime? PlannedLeaveDt, DateTime? LeavingDt);

/// <summary>
/// 아이 출발 이벤트 이력으로 출발 상태를 계산한다
/// </summary>
public static class ChildLeaveState
{
    public const string EventChildLeaving = "CHILD_LEAVING";
    public const string EventLeaveScheduled = "LEAVE_SCHEDULED";
    public const string EventLeaveCanceled = "LEAVE_CANCELED";
    public const string StatusNone = "NONE";
    public const string StatusScheduled = "SCHEDULED";
    public const string StatusSoon = "SOON";
    public const string StatusLeaving = "LEAVING";

    /// <summary>
    /// PICKUP_EVENT_ID 순의 이벤트 이력으로 아이 출발 상태를 요약한다
    /// </summary>
    public static ChildLeaveSummary Summarize(IReadOnlyList<LinkEventRow> events, DateTime now, int soonMin)
    {
        var leaving = events.FirstOrDefault(e => e.EventType == EventChildLeaving);
        if (leaving is not null)
            return new ChildLeaveSummary(StatusLeaving, null, leaving.EventDt);

        var last = events.LastOrDefault(e =>
            e.EventType == EventLeaveScheduled || e.EventType == EventLeaveCanceled);
        if (last is null || last.EventType == EventLeaveCanceled)
            return new ChildLeaveSummary(StatusNone, null, null);

        var leaveDt = ParseLeaveDt(last.Detail);
        if (leaveDt is null)
            return new ChildLeaveSummary(StatusNone, null, null);

        var status = now >= leaveDt.Value.AddMinutes(-soonMin) ? StatusSoon : StatusScheduled;
        return new ChildLeaveSummary(status, leaveDt, null);
    }

    /// <summary>
    /// 출발 예정 시각 DETAIL JSON을 만든다
    /// </summary>
    public static string BuildDetail(DateTime leaveDt)
    {
        return $"{{\"leaveDt\":\"{leaveDt.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture)}\"}}";
    }

    private static DateTime? ParseLeaveDt(string? detail)
    {
        if (string.IsNullOrEmpty(detail))
            return null;

        try
        {
            using var document = JsonDocument.Parse(detail);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("leaveDt", out var leaveDtElement)
                || leaveDtElement.ValueKind != JsonValueKind.String)
                return null;

            return DateTime.TryParseExact(leaveDtElement.GetString(), "yyyy-MM-ddTHH:mm:ss",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var leaveDt)
                ? leaveDt
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
