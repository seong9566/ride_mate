namespace RideMateService.Comm.Dto.v1.Schedule.Response;

/// <summary>
/// 시간표 한 건. 등록 응답과 시간표 목록이 같은 모양을 쓴다
/// </summary>
public class ScheduleItemDto
{
    /// <summary>
    /// 시간표 ID
    /// </summary>
    public int scheduleSeq { get; set; }

    /// <summary>
    /// 부모가 지은 학원 이름
    /// </summary>
    public string displayName { get; set; } = string.Empty;

    /// <summary>
    /// 학원 이름(장소 검색 결과의 이름)
    /// </summary>
    public string academyName { get; set; } = string.Empty;

    /// <summary>
    /// 학원 주소
    /// </summary>
    public string address { get; set; } = string.Empty;

    /// <summary>
    /// 학원 위도. 좌표 없이 등록한 학원이면 null
    /// </summary>
    public decimal? lat { get; set; }

    /// <summary>
    /// 학원 경도. 좌표 없이 등록한 학원이면 null
    /// </summary>
    public decimal? lng { get; set; }

    /// <summary>
    /// 하원 시각. HH:mm
    /// </summary>
    public string endTime { get; set; } = string.Empty;

    /// <summary>
    /// 요일 목록. 요일 순
    /// </summary>
    public List<ScheduleDayDto> days { get; set; } = new();
}

/// <summary>
/// 시간표의 요일 한 건
/// </summary>
public class ScheduleDayDto
{
    /// <summary>
    /// 시간표 요일 ID
    /// </summary>
    public int scheduleDaySeq { get; set; }

    /// <summary>
    /// 요일. 1(월)~7(일)
    /// </summary>
    public int dayOfWeek { get; set; }

    /// <summary>
    /// 그 요일의 픽업 스위치
    /// </summary>
    public bool pickupYn { get; set; }
}
