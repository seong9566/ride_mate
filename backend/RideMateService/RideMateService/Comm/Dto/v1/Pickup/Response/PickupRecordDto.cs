namespace RideMateService.Comm.Dto.v1.Pickup.Response;

/// <summary>
/// 픽업 완료 기록. 시간표 요일마다 최신 한 건이다
/// </summary>
public class PickupRecordDto
{
    /// <summary>
    /// 시간표 ID
    /// </summary>
    public int scheduleSeq { get; set; }

    /// <summary>
    /// 요일. 1(월)~7(일)
    /// </summary>
    public int dayOfWeek { get; set; }

    /// <summary>
    /// 집 도착 시각
    /// </summary>
    public DateTime arriveDt { get; set; }

    /// <summary>
    /// 총 소요 분
    /// </summary>
    public int totalMin { get; set; }

    /// <summary>
    /// 대기 분
    /// </summary>
    public int waitMin { get; set; }

    /// <summary>
    /// 출발 장소와 시각
    /// </summary>
    public PickupRecordPlaceDto depart { get; set; } = new();

    /// <summary>
    /// 탑승 장소와 시각
    /// </summary>
    public PickupRecordPlaceDto board { get; set; } = new();

    /// <summary>
    /// 다음부터 묻지 않고 저장
    /// </summary>
    public bool autoSave { get; set; }

    /// <summary>
    /// 마지막으로 저장한 시각
    /// </summary>
    public DateTime updateDt { get; set; }
}

/// <summary>
/// 픽업 완료 기록의 장소와 시각
/// </summary>
public class PickupRecordPlaceDto
{
    /// <summary>
    /// 장소 이름
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
    /// 시각
    /// </summary>
    public DateTime dt { get; set; }
}
