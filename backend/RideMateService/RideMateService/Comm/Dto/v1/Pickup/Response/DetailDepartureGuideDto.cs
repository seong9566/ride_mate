namespace RideMateService.Comm.Dto.v1.Pickup.Response;

/// <summary>
/// 출발 안내 계산 결과
/// </summary>
public class DetailDepartureGuideDto
{
    /// <summary>
    /// 대상 학원가 이름. 없으면 null
    /// </summary>
    public string? areaName { get; set; }

    /// <summary>
    /// 부모가 지정한 학원 이름
    /// </summary>
    public string academyName { get; set; } = string.Empty;

    /// <summary>
    /// 오늘 적용된 하원 시각(HH:mm)
    /// </summary>
    public string endTime { get; set; } = string.Empty;

    /// <summary>
    /// 픽업 장소. 쓸 수 있는 지점이 없으면 null
    /// </summary>
    public DepartureGuidePickupDto? pickup { get; set; }

    /// <summary>
    /// 아이 도착 시각. pickup이 null이면 null
    /// </summary>
    public DateTime? childArrivalDt { get; set; }

    /// <summary>
    /// 현재 위치 → 픽업 장소 → 집 차량 경로. 자기 계획이 대기 중이면 첫 구간 출발지는 현재 위치가 아니라 대기 중인 주차장이다. 집 좌표가 없으면 첫 구간뿐이다. pickup이 null이면 null
    /// </summary>
    public DepartureGuideDriveRouteDto? driveRoute { get; set; }

    /// <summary>
    /// 추천 출발 시각. 하원 시각보다 기본 10분 먼저 픽업 장소에 닿도록 구한다. pickup이 null이면 null
    /// </summary>
    public DateTime? departureDt { get; set; }

    /// <summary>
    /// 남은 여유(분). 음수면 늦었다
    /// </summary>
    public int? slackMin { get; set; }

    /// <summary>
    /// 픽업 장소 도착 예상 시각. 여유가 있으면 도착 목표 시각, 늦었으면 지금 출발해 닿는 시각. pickup이 null이면 null
    /// </summary>
    public DateTime? pickupArrivalDt { get; set; }

    /// <summary>
    /// 집 도착 예상 시각. 아이를 태운 뒤 집까지 간다고 본다. 집 구간이 없으면 null
    /// </summary>
    public DateTime? homeArrivalDt { get; set; }

    /// <summary>
    /// 픽업 장소의 단속 구역 판정
    /// </summary>
    public DepartureGuideEnforcementDto? enforcement { get; set; }

    /// <summary>
    /// 배정 근거. 해당 없으면 빈 배열
    /// </summary>
    public List<DepartureGuideReasonDto> reasons { get; set; } = new();

    /// <summary>
    /// 진행 중인 오늘 픽업 계획 ID. 계획이 없으면 null
    /// </summary>
    public int? pickupPlanSeq { get; set; }

    /// <summary>
    /// 계획의 배정 시각. 계획이 없거나 계획에 장소가 없으면 null
    /// </summary>
    public DateTime? slotDt { get; set; }

    /// <summary>
    /// 계획 상태 (PLANNED, STARTED, WAITING, STOPPED, LOOPING). 계획이 없으면 null
    /// </summary>
    public string? planStatus { get; set; }

    /// <summary>
    /// 자기 계획이 대기 중(WAITING)일 때 기다리는 주차장. 아니면 null
    /// </summary>
    public DepartureGuideWaitingPlaceDto? waitingPlace { get; set; }
}

/// <summary>
/// 출발 안내의 대기 중인 주차장
/// </summary>
public class DepartureGuideWaitingPlaceDto
{
    /// <summary>
    /// 주차장 ID
    /// </summary>
    public int parkingLotSeq { get; set; }

    /// <summary>
    /// 주차장 이름
    /// </summary>
    public string name { get; set; } = string.Empty;

    /// <summary>
    /// 주차장 위도
    /// </summary>
    public decimal lat { get; set; }

    /// <summary>
    /// 주차장 경도
    /// </summary>
    public decimal lng { get; set; }

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
    /// 1일 주차권 요금(원). 없으면 null
    /// </summary>
    public int? dayPassFee { get; set; }
}

/// <summary>
/// 출발 안내의 차량 경로
/// </summary>
public class DepartureGuideDriveRouteDto
{
    /// <summary>
    /// 전체 차량 소요 시간(분). legs의 driveMin 합이라 구간 시간을 더한 값과 같다. 지금 출발 기준
    /// </summary>
    public int driveMin { get; set; }

    /// <summary>
    /// 전체 경로 거리(m). source가 ESTIMATE면 null
    /// </summary>
    public int? distanceM { get; set; }

    /// <summary>
    /// ROUTE(카카오모빌리티 자동차 길찾기), ESTIMATE(직선 거리 추정)
    /// </summary>
    public string source { get; set; } = string.Empty;

    /// <summary>
    /// 구간별 결과. 현재 위치(자기 계획이 대기 중이면 대기 중인 주차장) → 픽업 장소 → 집 순서이고 집 좌표가 없으면 첫 구간뿐이다
    /// </summary>
    public List<DepartureGuideDriveLegDto> legs { get; set; } = new();
}

/// <summary>
/// 출발 안내 차량 경로의 한 구간
/// </summary>
public class DepartureGuideDriveLegDto
{
    /// <summary>
    /// 출발지 이름. 첫 구간은 '현재 위치'(자기 계획이 대기 중이면 대기 중인 주차장 이름), 둘째 구간은 픽업 장소 이름(없으면 '픽업 장소')
    /// </summary>
    public string from { get; set; } = string.Empty;

    /// <summary>
    /// 도착지 이름. 첫 구간은 픽업 장소 이름(없으면 '픽업 장소'), 둘째 구간은 '집'
    /// </summary>
    public string to { get; set; } = string.Empty;

    /// <summary>
    /// 구간 소요 시간(분, 올림)
    /// </summary>
    public int driveMin { get; set; }

    /// <summary>
    /// 구간 거리(m). source가 ESTIMATE면 null
    /// </summary>
    public int? distanceM { get; set; }
}

/// <summary>
/// 출발 안내의 픽업 장소
/// </summary>
public class DepartureGuidePickupDto
{
    /// <summary>
    /// 기본 지점이면 픽업 지점 ID, 임의 좌표면 null
    /// </summary>
    public int? pickupSpotSeq { get; set; }

    /// <summary>
    /// 기본 지점 이름. 계획의 임의 좌표면 계획에 저장된 장소 이름, 계획 없이 보낸 임의 좌표면 null
    /// </summary>
    public string? spotName { get; set; }

    /// <summary>
    /// 픽업 장소 위도
    /// </summary>
    public decimal lat { get; set; }

    /// <summary>
    /// 픽업 장소 경도
    /// </summary>
    public decimal lng { get; set; }

    /// <summary>
    /// 아이 도보 시간(분)
    /// </summary>
    public int walkMin { get; set; }

    /// <summary>
    /// 도보 시간 출처 (STORED, ROUTE, ESTIMATE)
    /// </summary>
    public string walkSource { get; set; } = string.Empty;

    /// <summary>
    /// 학원 → 픽업 장소 도보 경로 선. 경로가 없거나 ESTIMATE면 null
    /// </summary>
    public List<GeoPointDto>? walkPath { get; set; }
}

/// <summary>
/// 좌표 한 점
/// </summary>
public class GeoPointDto
{
    /// <summary>
    /// 위도
    /// </summary>
    public decimal lat { get; set; }

    /// <summary>
    /// 경도
    /// </summary>
    public decimal lng { get; set; }
}

/// <summary>
/// 단속 구역 판정
/// </summary>
public class DepartureGuideEnforcementDto
{
    /// <summary>
    /// 판정 결과 (RESTRICTED, CLEAR, UNKNOWN)
    /// </summary>
    public string status { get; set; } = string.Empty;

    /// <summary>
    /// 걸린 구역 유형(중복 제거). RESTRICTED가 아니면 빈 배열
    /// </summary>
    public List<string> zoneTypes { get; set; } = new();
}

/// <summary>
/// 배정 근거. 코드에 없는 값은 null이다
/// </summary>
public class DepartureGuideReasonDto
{
    /// <summary>
    /// 근거 코드 (ACADEMY_FRONT_RESTRICTED, HIGHER_SPOT_BLOCKED, NO_SPOT_AVAILABLE, CUSTOM_LOCATION, NO_ACADEMY_LOCATION,
    /// SLOT_SHIFTED, ROAD_CONGESTION). NO_ACADEMY_LOCATION은 학원 좌표가 없어 픽업 지점·도보·단속 판정을 할 수 없다는 뜻이며 pickup은 null이다.
    /// SLOT_SHIFTED는 계획의 배정 시각이 아이 도착 시각보다 늦다는 뜻이고, ROAD_CONGESTION은 학원가 진입 혼잡으로 출발 시각을 당겼다는 뜻이다
    /// </summary>
    public string code { get; set; } = string.Empty;

    /// <summary>
    /// ACADEMY_FRONT_RESTRICTED: 학원 좌표에 걸린 구역 유형
    /// </summary>
    public List<string>? zoneTypes { get; set; }

    /// <summary>
    /// HIGHER_SPOT_BLOCKED: 건너뛴 픽업 지점 ID
    /// </summary>
    public int? pickupSpotSeq { get; set; }

    /// <summary>
    /// HIGHER_SPOT_BLOCKED: 건너뛴 픽업 지점 이름
    /// </summary>
    public string? spotName { get; set; }

    /// <summary>
    /// HIGHER_SPOT_BLOCKED: 금지 사유
    /// </summary>
    public string? blockReason { get; set; }

    /// <summary>
    /// HIGHER_SPOT_BLOCKED: 금지 시작 시각(HH:mm)
    /// </summary>
    public string? blockStartTime { get; set; }

    /// <summary>
    /// HIGHER_SPOT_BLOCKED: 금지 종료 시각(HH:mm)
    /// </summary>
    public string? blockEndTime { get; set; }

    /// <summary>
    /// SLOT_SHIFTED: 배정 시각 − 아이 도착 시각(분)
    /// </summary>
    public int? shiftMin { get; set; }

    /// <summary>
    /// ROAD_CONGESTION: 출발 시각을 당긴 시간(분)
    /// </summary>
    public int? congestionMin { get; set; }

    /// <summary>
    /// ROAD_CONGESTION: 혼잡 보정을 적용한 학원가 코드
    /// </summary>
    public string? areaCode { get; set; }
}
