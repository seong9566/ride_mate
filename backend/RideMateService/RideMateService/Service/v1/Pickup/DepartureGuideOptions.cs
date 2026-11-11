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
    /// 추천 출발 시각에 더하는 여유 시간(분)
    /// </summary>
    public int BufferMin { get; set; } = 1;

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
}
