namespace RideMateService.Comm.Dto.v1.Pickup.Response;

/// <summary>
/// 대기 장소 추천 목록
/// </summary>
public class WaitingPlaceListDto
{
    /// <summary>
    /// 부모가 지정한 학원 이름
    /// </summary>
    public string academyName { get; set; } = string.Empty;

    /// <summary>
    /// 오늘 적용된 하원 시각(HH:mm)
    /// </summary>
    public string endTime { get; set; } = string.Empty;

    /// <summary>
    /// 학원 위도. 학원 좌표가 없으면 null
    /// </summary>
    public decimal? academyLat { get; set; }

    /// <summary>
    /// 학원 경도. 학원 좌표가 없으면 null
    /// </summary>
    public decimal? academyLng { get; set; }

    /// <summary>
    /// 픽업 장소. 쓸 수 있는 지점이 없으면 null
    /// </summary>
    public DepartureGuidePickupDto? pickup { get; set; }

    /// <summary>
    /// 남은 여유(분). 출발 안내의 slackMin과 같다(혼잡 보정 포함). 계산할 수 없으면 null
    /// </summary>
    public int? slackMin { get; set; }

    /// <summary>
    /// 추천 대기 장소. 해당 없으면 빈 배열
    /// </summary>
    public List<WaitingPlaceDto> places { get; set; } = new();

    /// <summary>
    /// 출발 안내의 배정 근거. 해당 없으면 빈 배열
    /// </summary>
    public List<DepartureGuideReasonDto> reasons { get; set; } = new();
}

/// <summary>
/// 추천 대기 장소 한 건
/// </summary>
public class WaitingPlaceDto
{
    /// <summary>
    /// 대기 장소(주차장) ID
    /// </summary>
    public int parkingLotSeq { get; set; }

    /// <summary>
    /// 대기 장소 이름
    /// </summary>
    public string name { get; set; } = string.Empty;

    /// <summary>
    /// 대기 장소 위도
    /// </summary>
    public decimal lat { get; set; }

    /// <summary>
    /// 대기 장소 경도
    /// </summary>
    public decimal lng { get; set; }

    /// <summary>
    /// 현재 위치에서 대기 장소까지 차량 소요 시간(분)
    /// </summary>
    public int driveMin { get; set; }

    /// <summary>
    /// 현재 위치에서 대기 장소까지 거리(m). 직선 거리에 도로 보정을 곱한 추정값
    /// </summary>
    public int distanceM { get; set; }

    /// <summary>
    /// 기본 요금 시간(분)
    /// </summary>
    public int baseMin { get; set; }

    /// <summary>
    /// 기본 요금(원)
    /// </summary>
    public int baseFee { get; set; }

    /// <summary>
    /// 추가 요금 단위 시간(분). 없으면 null
    /// </summary>
    public int? addUnitMin { get; set; }

    /// <summary>
    /// 추가 요금 단위 금액(원). 없으면 null
    /// </summary>
    public int? addFee { get; set; }

    /// <summary>
    /// 예상 주차 요금(원). 계산할 수 없으면 null
    /// </summary>
    public int? fee { get; set; }

    /// <summary>
    /// 예상 요금을 계산할 수 없을 때의 기본 요금 안내
    /// </summary>
    public string? feeText { get; set; }

    /// <summary>
    /// 1일 주차권 요금(원)
    /// </summary>
    public int? dayPassFee { get; set; }

    /// <summary>
    /// 빈자리 상태(AVAILABLE, FEW, UNKNOWN)
    /// </summary>
    public string space { get; set; } = string.Empty;

    /// <summary>
    /// 마지막으로 확인한 빈자리 수. 알 수 없으면 null
    /// </summary>
    public int? availableSpaces { get; set; }
}
