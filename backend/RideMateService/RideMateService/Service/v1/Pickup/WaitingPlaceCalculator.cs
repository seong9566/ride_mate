using System.Globalization;

namespace RideMateService.Service.v1.Pickup;

/// <summary>
/// 대기 장소의 시간·요금·운영 여부 계산(DB·외부 호출 없음)
/// </summary>
public static class WaitingPlaceCalculator
{
    private const string SpaceAvailable = "AVAILABLE";
    private const string SpaceFew = "FEW";
    private const string SpaceUnknown = "UNKNOWN";

    /// <summary>
    /// 대기 가능 시간 = 남은 여유 + 픽업까지 차량 시간 − 대기 장소까지 차량 시간 − 대기 장소에서 픽업까지 차량 시간
    /// </summary>
    public static int WaitMinutes(int slackMin, int driveToPickupMin, int driveToLotMin, int lotToPickupMin)
        => slackMin + driveToPickupMin - driveToLotMin - lotToPickupMin;

    /// <summary>
    /// 대기 시간에 따른 예상 요금을 계산한다
    /// </summary>
    public static int? Fee(int waitMin, short baseMin, int baseFee, short? addUnitMin, int? addFee, int? dayMaxFee)
    {
        if (addUnitMin is null || addFee is null || addUnitMin <= 0)
            return baseFee == 0 ? 0 : null;

        var fee = waitMin <= baseMin
            ? baseFee
            : baseFee + (int)Math.Ceiling((waitMin - baseMin) / (double)addUnitMin.Value) * addFee.Value;

        return dayMaxFee is > 0 ? Math.Min(fee, dayMaxFee.Value) : fee;
    }

    /// <summary>
    /// 기본 시간과 기본 요금을 표시용 문자열로 만든다
    /// </summary>
    public static string FeeText(short baseMin, int baseFee)
        => $"{baseMin}분 {baseFee.ToString("N0", CultureInfo.InvariantCulture)}원";

    /// <summary>
    /// 정렬에 사용할 예상 요금을 계산한다
    /// </summary>
    public static double SortFee(int? fee, int waitMin, short baseMin, int baseFee)
        => fee ?? (baseMin == 0 ? baseFee : baseFee / (double)baseMin * waitMin);

    /// <summary>
    /// 대기 시작부터 종료까지 운영 시간 안에 드는지 확인한다
    /// </summary>
    public static bool IsOpen(TimeSpan? open, TimeSpan? close, TimeSpan waitStart, TimeSpan endTime)
    {
        if (open is null || close is null)
            return false;

        var effectiveClose = close >= new TimeSpan(23, 59, 0) || close <= open
            ? TimeSpan.FromDays(1)
            : close.Value;
        return open <= waitStart && endTime <= effectiveClose;
    }

    /// <summary>
    /// 남은 공간 수를 가용 상태로 바꾼다
    /// </summary>
    public static string? Space(int? available, int fewThreshold)
    {
        if (available is null)
            return SpaceUnknown;
        if (available == 0)
            return null;

        return available < fewThreshold ? SpaceFew : SpaceAvailable;
    }
}
