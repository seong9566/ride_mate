namespace RideMateService.Comm.Dto.v1.Schedule.Response;

/// <summary>
/// 우리 픽업 장소 저장 결과
/// </summary>
public class UpdateSchedulePickupSpotResultDto
{
    /// <summary>
    /// 409일 때 까닭(INVALID_PICKUP_SPOT). 그 밖에는 null
    /// </summary>
    public string? reason { get; set; }

    /// <summary>
    /// 시간표 ID. 409면 null
    /// </summary>
    public int? scheduleSeq { get; set; }

    /// <summary>
    /// 저장한 뒤의 픽업 장소. 시간표 목록의 pickupSpot과 같은 모양이고, 후보가 없으면 null
    /// </summary>
    public SchedulePickupSpotDto? pickupSpot { get; set; }
}
