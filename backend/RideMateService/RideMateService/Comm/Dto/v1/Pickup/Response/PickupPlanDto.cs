namespace RideMateService.Comm.Dto.v1.Pickup.Response;

/// <summary>
/// 오늘 픽업 계획
/// </summary>
public class PickupPlanDto
{
    /// <summary>
    /// 409일 때만 사유 코드 (TOO_EARLY, NO_ACADEMY_LOCATION). 200이면 null
    /// </summary>
    public string? reason { get; set; }

    /// <summary>
    /// 오늘 계획 ID
    /// </summary>
    public int? pickupPlanSeq { get; set; }

    /// <summary>
    /// 계획 상태 (PLANNED, STARTED, WAITING, STOPPED, LOOPING)
    /// </summary>
    public string? status { get; set; }

    /// <summary>
    /// 픽업 날짜(yyyy-MM-dd)
    /// </summary>
    public string? pickupDate { get; set; }

    /// <summary>
    /// 그날 적용된 하원 시각(HH:mm)
    /// </summary>
    public string? endTime { get; set; }

    /// <summary>
    /// 부모가 지정한 학원 이름(출발 안내의 academyName과 같은 값)
    /// </summary>
    public string? academyName { get; set; }

    /// <summary>
    /// 배정 장소. 없으면 null
    /// </summary>
    public PickupPlanLocationDto? pickup { get; set; }

    /// <summary>
    /// 배정 도착 시각. pickup이 null이면 null
    /// </summary>
    public DateTime? slotDt { get; set; }

    /// <summary>
    /// 만료 시각
    /// </summary>
    public DateTime? expireDt { get; set; }

    /// <summary>
    /// 이번 요청으로 장소나 시각이 바뀌었는지. 계획 만들기는 false
    /// </summary>
    public bool reassigned { get; set; }
}

/// <summary>
/// 오늘 계획의 배정 장소
/// </summary>
public class PickupPlanLocationDto
{
    /// <summary>
    /// 장소 종류 (SPOT: 기본 지점, CUSTOM: 임의 좌표)
    /// </summary>
    public string locationType { get; set; } = string.Empty;

    /// <summary>
    /// 기본 지점 ID. CUSTOM이면 null
    /// </summary>
    public int? pickupSpotSeq { get; set; }

    /// <summary>
    /// 지점 이름 또는 임의 좌표의 장소 이름
    /// </summary>
    public string name { get; set; } = string.Empty;

    /// <summary>
    /// 위도
    /// </summary>
    public decimal lat { get; set; }

    /// <summary>
    /// 경도
    /// </summary>
    public decimal lng { get; set; }

    /// <summary>
    /// 아이 도보 시간(분)
    /// </summary>
    public int walkMin { get; set; }

    /// <summary>
    /// 기본 지점의 찾는 법. CUSTOM이면 null
    /// </summary>
    public string? guideText { get; set; }

    /// <summary>
    /// 기본 지점의 현장 사진 주소. CUSTOM이면 null
    /// </summary>
    public string? photoUrl { get; set; }
}
