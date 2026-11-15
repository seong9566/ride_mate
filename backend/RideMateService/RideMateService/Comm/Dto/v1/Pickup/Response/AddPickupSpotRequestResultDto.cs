namespace RideMateService.Comm.Dto.v1.Pickup.Response;

/// <summary>
/// 픽업 장소 추가 요청 접수 결과
/// </summary>
public class AddPickupSpotRequestResultDto
{
    /// <summary>
    /// 접수한 요청 ID
    /// </summary>
    public int pickupSpotRequestSeq { get; set; }
}
