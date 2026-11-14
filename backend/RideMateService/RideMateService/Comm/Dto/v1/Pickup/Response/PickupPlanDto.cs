namespace RideMateService.Comm.Dto.v1.Pickup.Response;

/// <summary>
/// 오늘 픽업 계획
/// </summary>
public class PickupPlanDto
{
    /// <summary>
    /// 409일 때만 사유 코드 (TOO_EARLY, NO_ACADEMY_LOCATION, PLAN_CLOSED, ALREADY_STOPPED, INVALID_STATUS, INVALID_PARKING_LOT). 200이면 null
    /// </summary>
    public string? reason { get; set; }

    /// <summary>
    /// 오늘 계획 ID
    /// </summary>
    public int? pickupPlanSeq { get; set; }

    /// <summary>
    /// 계획 상태 (PLANNED, STARTED, WAITING, STOPPED, LOOPING). 끝난 계획에 같은 요청을 다시 보낸 응답이면 끝난 상태(BOARDED, EXPIRED, CANCELED)
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
    /// 첫 정차 시각(오프셋 없는 KST). 정차 전이면 null
    /// </summary>
    public DateTime? stopDt { get; set; }

    /// <summary>
    /// 이번 요청으로 장소나 시각이 바뀌었는지. 계획 만들기는 false
    /// </summary>
    public bool reassigned { get; set; }

    /// <summary>
    /// 정차 판정 결과. 정차(UpdatePickupPlanStop) 응답에만 채우고 다른 API는 null
    /// </summary>
    public PickupStopCheckDto? stopCheck { get; set; }

    /// <summary>
    /// 취소 문자 결과 (NONE, SENT, FAILED). 부모 취소(DeletePickupPlan) 응답에만 채우고 다른 API는 null
    /// </summary>
    public string? cancelNotice { get; set; }

    /// <summary>
    /// 대기 중인 주차장 ID. 계획 상태가 WAITING일 때만 채우고 아니면 null(대기 뒤 출발하면 null)
    /// </summary>
    public int? parkingLotSeq { get; set; }

    /// <summary>
    /// 집으로 가는 길 화면 값. 탑승 기록(UpdatePickupPlanBoard) 응답에만 채우고 다른 API는 null
    /// </summary>
    public PickupHomeRouteDto? homeRoute { get; set; }
}

/// <summary>
/// 탑승 기록 응답의 집으로 가는 길 화면 값
/// </summary>
public class PickupHomeRouteDto
{
    /// <summary>
    /// 아이 이름. 읽지 못하면 null
    /// </summary>
    public string? childName { get; set; }

    /// <summary>
    /// 아이가 탑승한 시각(오프셋 없는 KST)
    /// </summary>
    public DateTime? boardedDt { get; set; }

    /// <summary>
    /// 탑승 장소 이름(기본 지점 이름 또는 임의 좌표의 장소 이름). 없으면 null
    /// </summary>
    public string? boardedPlaceName { get; set; }

    /// <summary>
    /// 집 주소. 집을 저장하지 않았으면 null
    /// </summary>
    public string? homeAddress { get; set; }

    /// <summary>
    /// 집 위도. 집을 저장하지 않았으면 null
    /// </summary>
    public decimal? homeLat { get; set; }

    /// <summary>
    /// 집 경도. 집을 저장하지 않았으면 null
    /// </summary>
    public decimal? homeLng { get; set; }

    /// <summary>
    /// 출발점에서 집까지 자동차로 걸리는 시간(분). 집 좌표나 출발점이 없으면 null
    /// </summary>
    public int? driveMin { get; set; }

    /// <summary>
    /// 출발점에서 집까지 거리(m). source가 ROUTE일 때만 채우고 ESTIMATE나 경로가 없으면 null
    /// </summary>
    public int? distanceM { get; set; }

    /// <summary>
    /// 집 도착 예정 시각(서버 현재 시각 + driveMin, 오프셋 없는 KST). driveMin이 null이면 null
    /// </summary>
    public DateTime? arrivalDt { get; set; }

    /// <summary>
    /// 경로 출처 (ROUTE: 카카오 자동차 길찾기, ESTIMATE: 직선 거리 추정). 경로가 없으면 null
    /// </summary>
    public string? source { get; set; }
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
