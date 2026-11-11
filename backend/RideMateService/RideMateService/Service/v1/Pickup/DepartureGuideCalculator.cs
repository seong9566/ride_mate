namespace RideMateService.Service.v1.Pickup;

/// <summary>
/// 출발 안내의 시각·시간 계산(DB·외부 호출 없음)
/// </summary>
public static class DepartureGuideCalculator
{
    /// <summary>
    /// 아이 도착 시각 = 날짜 + 하원 시각 + 정리 시간 + 도보 시간
    /// </summary>
    public static DateTime ChildArrival(DateOnly date, TimeSpan endTime, int tidyUpMin, int walkMin)
        => date.ToDateTime(TimeOnly.MinValue) + endTime + TimeSpan.FromMinutes(tidyUpMin + walkMin);

    /// <summary>
    /// 추천 출발 시각 = 아이 도착 시각 − 차량 소요 시간 − 여유 시간
    /// </summary>
    public static DateTime Departure(DateTime childArrival, int driveMin, int bufferMin)
        => childArrival - TimeSpan.FromMinutes(driveMin + bufferMin);

    /// <summary>
    /// 남은 여유(분) = 추천 출발 시각 − 현재 시각을 분 단위로 내림. 음수면 늦었다
    /// </summary>
    public static int SlackMinutes(DateTime departure, DateTime now)
        => (int)Math.Floor((departure - now).TotalMinutes);

    /// <summary>
    /// 거리(m) × 보정 계수 ÷ 속도(km/h)를 분으로 올림한다. 최소 1분
    /// </summary>
    public static int EstimateMinutes(double meters, double kmh, double factor = 1.0)
        => Math.Max(1, (int)Math.Ceiling(meters * factor / (kmh * 1000 / 60)));

    /// <summary>
    /// 초를 분으로 올림한다. 최소 1분
    /// </summary>
    public static int SecondsToMinutesCeil(int seconds)
        => Math.Max(1, (int)Math.Ceiling(seconds / 60.0));

    /// <summary>
    /// 요일 유형. 공휴일이나 일요일은 HOLIDAY, 토요일은 SAT, 그 밖은 WEEKDAY
    /// </summary>
    public static string DayType(DateOnly date, bool isHoliday)
    {
        if (isHoliday || date.DayOfWeek == System.DayOfWeek.Sunday)
            return "HOLIDAY";

        return date.DayOfWeek == System.DayOfWeek.Saturday ? "SAT" : "WEEKDAY";
    }

    /// <summary>
    /// TIMETABLE_DAY.DAY_OF_WEEK 값. 월 1 ~ 일 7
    /// </summary>
    public static byte DayOfWeek(DateOnly date)
        => date.DayOfWeek == System.DayOfWeek.Sunday ? (byte)7 : (byte)date.DayOfWeek;
}
