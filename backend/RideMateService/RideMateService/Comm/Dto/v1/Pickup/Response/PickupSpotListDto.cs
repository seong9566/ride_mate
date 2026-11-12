namespace RideMateService.Comm.Dto.v1.Pickup.Response;

/// <summary>
/// 픽업 장소 후보 목록
/// </summary>
public class PickupSpotListDto
{
    /// <summary>
    /// 시간표 ID
    /// </summary>
    public int scheduleSeq { get; set; }

    /// <summary>
    /// 부모가 지정한 학원 이름
    /// </summary>
    public string academyName { get; set; } = string.Empty;

    /// <summary>
    /// 평가한 하원 시각(HH:mm). 그날 하원 시각 변경 예외가 있으면 바뀐 시각
    /// </summary>
    public string endTime { get; set; } = string.Empty;

    /// <summary>
    /// 학원가 이름. 학원가가 없으면 null
    /// </summary>
    public string? areaName { get; set; }

    /// <summary>
    /// 후보가 없는 까닭. NO_ACADEMY_LOCATION(학원 좌표·학원가 없음), NO_SPOT_CANDIDATE(후보 지점 없음). 후보가 있으면 null
    /// </summary>
    public string? reason { get; set; }

    /// <summary>
    /// 학원 앞이 단속 구역이면 true, 아니면 false. 판정할 수 없으면 null
    /// </summary>
    public bool? academyFrontRestricted { get; set; }

    /// <summary>
    /// 후보 지점. 추천 지점이 맨 앞이고 나머지는 우리 픽업 장소 우선, 우선순위 순. 없으면 빈 배열
    /// </summary>
    public List<PickupSpotItemDto> spots { get; set; } = new();
}

/// <summary>
/// 픽업 장소 후보 한 건
/// </summary>
public class PickupSpotItemDto
{
    /// <summary>
    /// 픽업 지점 ID
    /// </summary>
    public int pickupSpotSeq { get; set; }

    /// <summary>
    /// 지점 이름
    /// </summary>
    public string spotName { get; set; } = string.Empty;

    /// <summary>
    /// 지점 위도
    /// </summary>
    public decimal lat { get; set; }

    /// <summary>
    /// 지점 경도
    /// </summary>
    public decimal lng { get; set; }

    /// <summary>
    /// 찾는 법. 없으면 null
    /// </summary>
    public string? guideText { get; set; }

    /// <summary>
    /// 현장 사진 주소. 없으면 null
    /// </summary>
    public string? photoUrl { get; set; }

    /// <summary>
    /// 학원 기준 지점 한 줄 안내. 없으면 null
    /// </summary>
    public string? note { get; set; }

    /// <summary>
    /// 학원에서 지점까지 아이 도보 시간(분)
    /// </summary>
    public int walkMin { get; set; }

    /// <summary>
    /// 학원에서 지점까지 도보 경로. 없으면 null
    /// </summary>
    public List<GeoPointDto>? walkPath { get; set; }

    /// <summary>
    /// 학원에서 지점까지 직선 거리(m)
    /// </summary>
    public int distanceM { get; set; }

    /// <summary>
    /// 추천 지점이면 true. 많아야 한 곳
    /// </summary>
    public bool recommended { get; set; }

    /// <summary>
    /// 우리 픽업 장소(부모가 고른 기본 지점)이면 true
    /// </summary>
    public bool preferred { get; set; }

    /// <summary>
    /// 그 시각에 쓸 수 있으면 true
    /// </summary>
    public bool available { get; set; }

    /// <summary>
    /// 쓸 수 없는 까닭. BLOCKED(금지 시간), FULL(정원 참). 쓸 수 있으면 null
    /// </summary>
    public string? unavailableReason { get; set; }

    /// <summary>
    /// 금지 사유. BLOCKED가 아니면 null
    /// </summary>
    public string? blockReason { get; set; }

    /// <summary>
    /// 금지 시작 시각(HH:mm). BLOCKED가 아니면 null
    /// </summary>
    public string? blockStartTime { get; set; }

    /// <summary>
    /// 금지 종료 시각(HH:mm). BLOCKED가 아니면 null
    /// </summary>
    public string? blockEndTime { get; set; }

    /// <summary>
    /// 아이 도착 시각에 붐비면 true(진행 중 계획 수가 정원 이상). 금지 시간이면 false
    /// </summary>
    public bool busy { get; set; }
}
