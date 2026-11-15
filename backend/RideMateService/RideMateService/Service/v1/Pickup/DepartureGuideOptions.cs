namespace RideMateService.Service.v1.Pickup;

/// <summary>
/// 출발 안내 계산 설정. 설정 절 "DepartureGuide".
/// </summary>
public class DepartureGuideOptions
{
    /// <summary>
    /// 하원 뒤 아이가 짐을 정리하는 시간(분)
    /// </summary>
    public int TidyUpMin { get; set; } = 1;

    /// <summary>
    /// 대기 여유(분). 픽업 장소 도착 목표는 하원 시각 + 아이 도보 시간에서 이만큼 뺀 시각이다
    /// </summary>
    public int WaitBeforeChildMin { get; set; } = 10;

    /// <summary>
    /// 도보 시간을 추정할 때 쓰는 아이 걷는 속도(km/h)
    /// </summary>
    public double ChildWalkKmh { get; set; } = 4.0;

    /// <summary>
    /// 차량 소요 시간을 추정할 때 쓰는 평균 속도(km/h)
    /// </summary>
    public double DriveKmh { get; set; } = 20;

    /// <summary>
    /// 직선 거리를 도로 거리로 바꾸는 보정 계수
    /// </summary>
    public double RoadFactor { get; set; } = 1.3;

    /// <summary>
    /// 하원 시각 뒤 이 시간(시간)이 지나기 전까지를 오늘 남은 픽업으로 본다.
    /// </summary>
    public int PickupWindowHours { get; set; } = 1;

    /// <summary>
    /// 학원가 진입 혼잡 보정 설정
    /// </summary>
    public DepartureCongestionOptions Congestion { get; set; } = new();
}

/// <summary>
/// 학원가 진입 혼잡 보정 설정. 설정 절 "DepartureGuide:Congestion".
/// </summary>
public class DepartureCongestionOptions
{
    /// <summary>
    /// 혼잡 보정 사용 여부
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 학원가별 진입 구간 거리(m)
    /// </summary>
    public Dictionary<string, int> EntryDistanceM { get; set; } = new();

    /// <summary>
    /// 최대 혼잡 보정 시간(분)
    /// </summary>
    public int MaxCongestionMin { get; set; } = 15;

    /// <summary>
    /// 수집 속도를 사용할 최소 표본 일수
    /// </summary>
    public int MinSampleDays { get; set; } = 3;
}
