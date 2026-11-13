namespace RideMateService.Comm.Dto.v1.Pickup.Response;

/// <summary>
/// 오늘의 픽업 경로 조회 결과. 현재 위치 → 학원은 차량이 내비로 구하고, 학원 → 집은 서버가 구한다
/// </summary>
public class DetailTodayPickupRouteDto
{
    /// <summary>
    /// 아이 이름
    /// </summary>
    public string childName { get; set; } = string.Empty;

    /// <summary>
    /// 부모가 지은 학원 이름
    /// </summary>
    public string displayName { get; set; } = string.Empty;

    /// <summary>
    /// 오늘 적용된 하원 시각(HH:mm). 오늘 시각 변경 예외가 있으면 바뀐 시각이다
    /// </summary>
    public string endTime { get; set; } = string.Empty;

    /// <summary>
    /// 학원가 이름. 학원이 학원가 밖이면 null
    /// </summary>
    public string? areaName { get; set; }

    /// <summary>
    /// 학원. 차량은 이 좌표로 현재 위치 → 학원 시간을 구한다
    /// </summary>
    public TodayPickupRouteAcademyDto academy { get; set; } = new();

    /// <summary>
    /// 저장된 집. 집 주소가 없으면 null
    /// </summary>
    public TodayPickupRouteHomeDto? home { get; set; }

    /// <summary>
    /// 학원 → 집 차량 경로. 학원이나 집 좌표가 없으면 null
    /// </summary>
    public TodayPickupHomeDriveDto? homeDrive { get; set; }
}

/// <summary>
/// 오늘의 픽업 경로의 학원
/// </summary>
public class TodayPickupRouteAcademyDto
{
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
}

/// <summary>
/// 오늘의 픽업 경로의 집
/// </summary>
public class TodayPickupRouteHomeDto
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
/// 학원 → 집 차량 경로
/// </summary>
public class TodayPickupHomeDriveDto
{
    /// <summary>
    /// 차량 소요 시간(분). 지금 출발 기준
    /// </summary>
    public int driveMin { get; set; }

    /// <summary>
    /// 경로 거리(m). source가 ESTIMATE면 null
    /// </summary>
    public int? distanceM { get; set; }

    /// <summary>
    /// ROUTE(카카오모빌리티 자동차 길찾기), ESTIMATE(직선 거리 추정)
    /// </summary>
    public string source { get; set; } = string.Empty;

    /// <summary>
    /// 학원 → 집 경로 선. source가 ESTIMATE면 null
    /// </summary>
    public List<GeoPointDto>? path { get; set; }
}
