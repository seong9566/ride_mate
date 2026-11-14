namespace RideMateService.Service.v1.Pickup;

/// <summary>
/// PickupLive 설정. 차량 위치와 실시간 구독을 서버 메모리에서 관리한다
/// </summary>
public class PickupLiveOptions
{
    /// <summary>
    /// 차 위치를 신선하다고 보는 시간(초). 정차한 계획은 이 시간이 지나도 위치를 보여 준다
    /// </summary>
    public int VehicleStaleSec { get; set; } = 30;

    /// <summary>
    /// 정차 뒤 차 위치를 메모리에 두는 최대 시간(분). 계획이 끝나면 그 전에 지운다
    /// </summary>
    public int VehicleParkedMaxMin { get; set; } = 60;

    /// <summary>
    /// 한 계획의 위치 보고 최소 간격(초)
    /// </summary>
    public int PositionMinIntervalSec { get; set; } = 1;

    /// <summary>
    /// 스트림 하트비트 간격(초)
    /// </summary>
    public int HeartbeatSec { get; set; } = 15;

    /// <summary>
    /// 스트림 최대 유지 시간(분)
    /// </summary>
    public int StreamMaxLifetimeMin { get; set; } = 30;

    /// <summary>
    /// 계획별 최대 구독 수. 아이·차량 대상마다 따로 센다
    /// </summary>
    public int MaxStreamsPerPlan { get; set; } = 5;

    /// <summary>
    /// 차 위치 기능 스위치. 끄면 보고를 버리고 아이에게 좌표를 내보내지 않는다
    /// </summary>
    public bool VehiclePositionEnabled { get; set; } = true;
}
