using RideMateService.Repository.v1.Pickup;

namespace RideMateService.Service.v1.Pickup;

/// <summary>
/// 학원가 진입 혼잡 보정 계산(DB·외부 호출 없음)
/// </summary>
public static class CongestionCalculator
{
    /// <summary>
    /// 시각을 자정부터 지난 분 기준 5분 칸으로 바꾼다
    /// </summary>
    public static short SlotMin(DateTime t)
        => (short)(t.Hour * 60 + t.Minute / 5 * 5);

    /// <summary>
    /// 계획·현재 칸에 함께 존재하는 같은 출처의 대표 속도를 고른다
    /// </summary>
    public static (decimal PlanKmh, decimal NowKmh, string Source)? PickSpeeds(
        IReadOnlyList<TrafficProfileRow> rows, short planSlot, short nowSlot, int minSampleDays)
    {
        var collectedPlan = rows.FirstOrDefault(x => x.Source == "COLLECTED"
                                                     && x.SlotMin == planSlot
                                                     && x.SampleDays >= minSampleDays);
        var collectedNow = rows.FirstOrDefault(x => x.Source == "COLLECTED"
                                                    && x.SlotMin == nowSlot
                                                    && x.SampleDays >= minSampleDays);
        if (collectedPlan is not null && collectedNow is not null)
            return (collectedPlan.SpeedKmh, collectedNow.SpeedKmh, "COLLECTED");

        var monthlyPlan = rows.FirstOrDefault(x => x.Source == "MONTHLY"
                                                   && x.SlotMin == planSlot
                                                   && x.SampleDays >= 1);
        var monthlyNow = rows.FirstOrDefault(x => x.Source == "MONTHLY"
                                                  && x.SlotMin == nowSlot
                                                  && x.SampleDays >= 1);
        if (monthlyPlan is not null && monthlyNow is not null)
            return (monthlyPlan.SpeedKmh, monthlyNow.SpeedKmh, "MONTHLY");

        return null;
    }

    /// <summary>
    /// 계획 칸과 현재 칸의 진입 구간 소요 시간 차이를 0부터 최대 보정 시간 사이로 자른다
    /// </summary>
    public static int Minutes(int entryDistanceM, decimal planKmh, decimal nowKmh, int maxMin)
    {
        var distanceKm = entryDistanceM / 1000m;
        var minutes = (int)Math.Ceiling(distanceKm / planKmh * 60 - distanceKm / nowKmh * 60);
        return Math.Clamp(minutes, 0, maxMin);
    }
}
