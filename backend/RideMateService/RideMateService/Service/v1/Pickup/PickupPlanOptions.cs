namespace RideMateService.Service.v1.Pickup;

/// <summary>
/// 오늘 픽업 계획 설정. 설정 절 "PickupPlan".
/// </summary>
public class PickupPlanOptions
{
    /// <summary>
    /// 하원 시각 몇 시간 전부터 계획을 만들 수 있는지(시간)
    /// </summary>
    public int CreateLeadHours { get; set; } = 3;

    /// <summary>
    /// 정원을 셀 때 배정 시각 앞뒤로 보는 간격(분)
    /// </summary>
    public int SlotWindowMin { get; set; } = 2;

    /// <summary>
    /// 지점이 꽉 찼을 때 배정 시각을 1분씩 늦춰 보는 한도(분)
    /// </summary>
    public int SlotShiftMaxMin { get; set; } = 2;

    /// <summary>
    /// 만료 시각 = 배정 시각(없으면 그날 하원 시각) + 이 값(시간)
    /// </summary>
    public int ExpireAfterHours { get; set; } = 1;

    /// <summary>
    /// 만료 주기 작업의 간격(초)
    /// </summary>
    public int ExpireIntervalSec { get; set; } = 60;

    /// <summary>
    /// 정차 위치가 배정 장소에서 이 거리(m) 안이면 배정 장소에 선 것으로 본다
    /// </summary>
    public int StopRadiusM { get; set; } = 30;
}
