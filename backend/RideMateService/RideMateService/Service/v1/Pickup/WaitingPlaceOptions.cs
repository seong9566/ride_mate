namespace RideMateService.Service.v1.Pickup;

/// <summary>
/// 대기 장소 추천 설정. 설정 절 "WaitingPlace".
/// </summary>
public class WaitingPlaceOptions
{
    /// <summary>
    /// 실시간 빈자리 API 제한 시간(ms)
    /// </summary>
    public int RealtimeTimeoutMs { get; set; } = 2000;

    /// <summary>
    /// 저장된 실시간 빈자리 값을 그대로 쓰는 시간(초)
    /// </summary>
    public int RealtimeCacheSec { get; set; } = 300;

    /// <summary>
    /// 빈자리가 적음(FEW)으로 표시되는 기준
    /// </summary>
    public int FewThreshold { get; set; } = 10;

    /// <summary>
    /// 대기 장소에서 픽업 장소까지 허용하는 최대 차량 시간(분)
    /// </summary>
    public int MaxLotToPickupMin { get; set; } = 5;

    /// <summary>
    /// 응답에 포함할 최대 대기 장소 수
    /// </summary>
    public int MaxPlaces { get; set; } = 3;
}
