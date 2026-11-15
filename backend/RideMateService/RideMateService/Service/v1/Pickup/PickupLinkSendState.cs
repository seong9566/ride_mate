using System.Text.Json;
using RideMateService.Repository.v1.Pickup;

namespace RideMateService.Service.v1.Pickup;

/// <summary>
/// 링크·다시 알림 발송 이벤트(LINK_SENT·REMIND_SENT)의 PICKUP_EVENT.DETAIL. success가 null이면 결과를 기다리는 진행 중 발송이다
/// </summary>
public sealed record SendDetail(string channel, bool? success, string? resultCode);

/// <summary>
/// 한 이벤트 유형의 발송 상태 요약
/// </summary>
/// <param name="Status">NONE(시도 없음), SENDING(진행 중), SENT(접수됨), FAILED(마지막 시도 실패)</param>
/// <param name="SentDt">처음 성공한 발송의 EVENT_DT</param>
/// <param name="Attempts">시도 횟수(성공·실패·진행 중을 모두 센다)</param>
/// <param name="RemainingAttempts">남은 시도 횟수. SENT면 0</param>
/// <param name="HasLiveInFlight">시간이 지나지 않은 진행 중 발송이 있는지</param>
public sealed record SendSummary(string Status, DateTime? SentDt, int Attempts, int RemainingAttempts, bool HasLiveInFlight);

/// <summary>
/// 발송 이벤트 이력으로 발송 상태를 계산한다
/// </summary>
public static class PickupLinkSendState
{
    public const string StatusNone = "NONE";
    public const string StatusSending = "SENDING";
    public const string StatusSent = "SENT";
    public const string StatusFailed = "FAILED";

    /// <summary>
    /// eventType 이벤트만 보고 요약한다. 성공한 것이 하나라도 있으면 SENT, 아니면 마지막 시도 기준이다.
    /// 결과가 없고 CREATE_DT가 now - inFlightTimeoutSec보다 이른 진행 중 발송은 실패로 센다
    /// </summary>
    public static SendSummary Summarize(IReadOnlyList<LinkEventRow> events, string eventType, DateTime now,
        int inFlightTimeoutSec, int maxAttempts)
    {
        var staleBefore = now.AddSeconds(-inFlightTimeoutSec);
        var attempts = 0;
        DateTime? sentDt = null;
        var hasLiveInFlight = false;
        var lastIsLive = false;

        foreach (var row in events.Where(e => e.EventType == eventType))
        {
            attempts++;
            var success = ParseDetail(row.Detail)?.success;
            if (success == true && sentDt is null)
                sentDt = row.EventDt;

            lastIsLive = success is null && row.CreateDt >= staleBefore;
            hasLiveInFlight |= lastIsLive;
        }

        string status;
        if (sentDt is not null)
            status = StatusSent;
        else if (attempts == 0)
            status = StatusNone;
        else
            status = lastIsLive ? StatusSending : StatusFailed;

        var remaining = status == StatusSent ? 0 : Math.Max(0, maxAttempts - attempts);
        return new SendSummary(status, sentDt, attempts, remaining, hasLiveInFlight);
    }

    /// <summary>
    /// DETAIL JSON을 읽는다. 값이 없으면 null
    /// </summary>
    public static SendDetail? ParseDetail(string? detail)
    {
        return string.IsNullOrEmpty(detail) ? null : JsonSerializer.Deserialize<SendDetail>(detail);
    }

    /// <summary>
    /// DETAIL JSON을 만든다. 키는 channel·success·resultCode다
    /// </summary>
    public static string ToJson(SendDetail detail)
    {
        return JsonSerializer.Serialize(detail);
    }
}
