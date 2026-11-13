namespace RideMateService.Comm.Dto.v1.Schedule.Response;

/// <summary>
/// 홈 화면 조회 결과. 아이·집과 월~일 요일별 시간표를 한 번에 담는다
/// </summary>
public class DetailHomeDto
{
    /// <summary>
    /// 등록된 아이. 살아 있는 아이가 없으면 null
    /// </summary>
    public HomeChildDto? child { get; set; }

    /// <summary>
    /// 저장된 집. 집 주소가 없으면 null
    /// </summary>
    public HomeLocationDto? home { get; set; }

    /// <summary>
    /// 월(1)~일(7) 7칸. 시간표가 없는 요일도 빈 items로 들어간다
    /// </summary>
    public List<HomeWeekDayDto> week { get; set; } = new();
}

/// <summary>
/// 홈 화면의 아이
/// </summary>
public class HomeChildDto
{
    /// <summary>
    /// 아이 ID
    /// </summary>
    public int childSeq { get; set; }

    /// <summary>
    /// 아이 이름
    /// </summary>
    public string childName { get; set; } = string.Empty;
}

/// <summary>
/// 홈 화면의 집
/// </summary>
public class HomeLocationDto
{
    /// <summary>
    /// 집 주소
    /// </summary>
    public string address { get; set; } = string.Empty;

    /// <summary>
    /// 집 위도. 좌표 없이 저장했으면 null
    /// </summary>
    public decimal? lat { get; set; }

    /// <summary>
    /// 집 경도. 좌표 없이 저장했으면 null
    /// </summary>
    public decimal? lng { get; set; }
}

/// <summary>
/// 요일 한 칸
/// </summary>
public class HomeWeekDayDto
{
    /// <summary>
    /// 요일. 1(월)~7(일)
    /// </summary>
    public int dayOfWeek { get; set; }

    /// <summary>
    /// 그 요일의 시간표. 하원 시각 순(같으면 시간표 ID 순). 픽업을 끈 시간표도 pickupYn = false로 들어간다
    /// </summary>
    public List<HomeScheduleDto> items { get; set; } = new();
}

/// <summary>
/// 요일 칸의 시간표 한 건
/// </summary>
public class HomeScheduleDto
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
    /// 그 요일의 픽업 스위치
    /// </summary>
    public bool pickupYn { get; set; }
}
