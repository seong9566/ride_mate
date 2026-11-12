namespace RideMateService.Comm.Dto.v1.Pickup.Response;

/// <summary>
/// 차 위치 보고 응답
/// </summary>
public class PickupPlanPositionDto
{
    /// <summary>
    /// 409일 때만 PLAN_CLOSED(만료) 또는 NOT_STARTED(경로 시작 전)
    /// </summary>
    public string? reason { get; set; }
}
