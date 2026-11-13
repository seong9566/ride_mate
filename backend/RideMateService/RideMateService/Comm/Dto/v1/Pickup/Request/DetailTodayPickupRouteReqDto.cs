using System.ComponentModel.DataAnnotations;

namespace RideMateService.Comm.Dto.v1.Pickup.Request;

/// <summary>
/// 오늘의 픽업 경로 조회 요청(JSON 본문)
/// </summary>
public class DetailTodayPickupRouteReqDto : IValidatableObject
{
    /// <summary>
    /// 경유지 최대 개수(카카오모빌리티 자동차 길찾기 한도)
    /// </summary>
    public const int MaxWaypoints = 5;

    /// <summary>
    /// 고른 시간표 ID(이번 주 스케줄의 scheduleSeq)
    /// </summary>
    [Required]
    [Range(1, int.MaxValue)]
    public int? scheduleSeq { get; set; }

    /// <summary>
    /// 고른 요일. 1(월)~7(일). 서버의 오늘 요일과 다르면 code 101이다
    /// </summary>
    [Required]
    [Range(1, 7)]
    public int? dayOfWeek { get; set; }

    /// <summary>
    /// 학원과 집 사이에 들를 곳. 보낸 순서대로 학원 → 경유지 → 집으로 계산한다. 최대 5개, 빠지면 경유지 없음
    /// </summary>
    public List<TodayPickupWaypointReqDto>? waypoints { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (waypoints is null)
            yield break;

        if (waypoints.Count > MaxWaypoints)
            yield return new ValidationResult($"waypoints는 {MaxWaypoints}개까지 보낼 수 있습니다.", new[] { nameof(waypoints) });

        for (var i = 0; i < waypoints.Count; i++)
        {
            if (!IsValidWaypoint(waypoints[i]))
                yield return new ValidationResult(
                    "경유지는 lat·lng를 함께 보내거나 address를 보내야 합니다.", new[] { $"{nameof(waypoints)}[{i}]" });
        }
    }

    /// <summary>
    /// 좌표 쌍이 다 있거나(주소는 무시) 좌표가 둘 다 없고 주소가 있어야 한다
    /// </summary>
    public static bool IsValidWaypoint(TodayPickupWaypointReqDto? waypoint)
    {
        if (waypoint is null)
            return false;
        if ((waypoint.lat is null) != (waypoint.lng is null))
            return false;
        return waypoint.lat is not null || !string.IsNullOrWhiteSpace(waypoint.address);
    }
}

/// <summary>
/// 경유지 한 곳. lat·lng를 함께 보내거나 address만 보낸다. 둘 다 보내면 좌표를 쓴다
/// </summary>
public class TodayPickupWaypointReqDto
{
    /// <summary>
    /// 위도. lng와 함께 보낸다
    /// </summary>
    [Range(-90.0, 90.0)]
    public decimal? lat { get; set; }

    /// <summary>
    /// 경도. lat와 함께 보낸다
    /// </summary>
    [Range(-180.0, 180.0)]
    public decimal? lng { get; set; }

    /// <summary>
    /// 경유지 이름(예: 강남우체국). 구간의 from·to에 쓴다. 빠지면 주소, 주소도 없으면 '경유지 n'이다. 앞뒤 공백을 지운 뒤 100자까지
    /// </summary>
    [TrimmedMaxLength(100)]
    public string? name { get; set; }

    /// <summary>
    /// 주소(예: 강남구 개포로 619). 좌표가 없으면 서버가 카카오 주소 검색으로 좌표를 찾는다. 앞뒤 공백을 지운 뒤 200자까지
    /// </summary>
    [TrimmedMaxLength(200)]
    public string? address { get; set; }
}
