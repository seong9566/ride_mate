namespace RideMateService.Comm.Dto.v1.Pickup.Response;

/// <summary>
/// 단속 구역 목록 조회 결과
/// </summary>
public class GetRestrictedZoneList
{
    /// <summary>
    /// 단속 구역 목록 버전
    /// </summary>
    public string version { get; set; } = string.Empty;

    /// <summary>
    /// 학원가별 단속 구역 목록. 버전이 같으면 null
    /// </summary>
    public List<RestrictedZoneAreaDto>? areas { get; set; }
}

/// <summary>
/// 학원가별 단속 구역 목록
/// </summary>
public class RestrictedZoneAreaDto
{
    /// <summary>
    /// 학원가 ID
    /// </summary>
    public int areaSeq { get; set; }

    /// <summary>
    /// 학원가 이름
    /// </summary>
    public string areaName { get; set; } = string.Empty;

    /// <summary>
    /// 단속 구역 목록
    /// </summary>
    public List<RestrictedZoneDto> zones { get; set; } = new();
}

/// <summary>
/// 단속 구역 한 건
/// </summary>
public class RestrictedZoneDto
{
    /// <summary>
    /// 단속 구역 ID
    /// </summary>
    public int restrictedZoneSeq { get; set; }

    /// <summary>
    /// 단속 구역 유형
    /// </summary>
    public string zoneType { get; set; } = string.Empty;

    /// <summary>
    /// 적용 요일 유형. 매일이면 null
    /// </summary>
    public string? dayType { get; set; }

    /// <summary>
    /// 적용 시작 시각(HH:mm). 종일이면 null
    /// </summary>
    public string? activeStartTime { get; set; }

    /// <summary>
    /// 적용 종료 시각(HH:mm). 종일이면 null
    /// </summary>
    public string? activeEndTime { get; set; }

    /// <summary>
    /// 단속 구역 면 목록
    /// </summary>
    public List<ZonePolygonDto> polygons { get; set; } = new();
}

/// <summary>
/// 외곽 링과 내부 링으로 구성된 단속 구역 면
/// </summary>
public class ZonePolygonDto
{
    /// <summary>
    /// 외곽 링 좌표 목록
    /// </summary>
    public List<GeoPointDto> outer { get; set; } = new();

    /// <summary>
    /// 내부 링별 좌표 목록
    /// </summary>
    public List<List<GeoPointDto>> holes { get; set; } = new();
}
