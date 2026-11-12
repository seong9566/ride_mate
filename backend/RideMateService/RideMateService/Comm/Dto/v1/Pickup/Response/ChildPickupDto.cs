namespace RideMateService.Comm.Dto.v1.Pickup.Response;

/// <summary>
/// 아이 픽업 안내
/// </summary>
public class ChildPickupDto
{
    /// <summary>
    /// ACTIVE(진행 중), BOARDED(탑승으로 끝남), CLOSED(만료·취소로 끝남, 만료 시각 지남, 링크 닫힘). ACTIVE가 아니면 linkStatus·serverDt 외에는 모두 null
    /// </summary>
    public string linkStatus { get; set; } = string.Empty;

    /// <summary>
    /// 서버 현재 시각(KST). 웹이 남은 시간을 계산할 때 쓴다
    /// </summary>
    public DateTime serverDt { get; set; }

    /// <summary>
    /// 아이 화면에 보이는 호칭(운전자 이름)
    /// </summary>
    public string? driverName { get; set; }

    /// <summary>
    /// 차 정보
    /// </summary>
    public ChildPickupCarDto? car { get; set; }

    /// <summary>
    /// 학원 위치(도보 경로 시작점). 학원 좌표가 없으면 null
    /// </summary>
    public GeoPointDto? academy { get; set; }

    /// <summary>
    /// 픽업 장소. 차량 계획 응답의 pickup과 같은 모양. 배정 장소가 없으면 null
    /// </summary>
    public PickupPlanLocationDto? pickup { get; set; }

    /// <summary>
    /// 학원에서 픽업 장소까지 도보 경로 선. 없으면 null
    /// </summary>
    public List<GeoPointDto>? walkPath { get; set; }

    /// <summary>
    /// 계획 상태 PLANNED·STARTED·WAITING·STOPPED·LOOPING
    /// </summary>
    public string? planStatus { get; set; }

    /// <summary>
    /// planStatus가 STOPPED일 때만 값. 마지막 정차가 배정 장소 안(STOPPED)이면 true, 밖(WRONG_STOP)이면 false
    /// </summary>
    public bool? stopAtSpot { get; set; }

    /// <summary>
    /// 배정 도착 시각
    /// </summary>
    public DateTime? slotDt { get; set; }

    /// <summary>
    /// 화면에 보여줄 도착 예정 시각. 지금은 배정 도착 시각과 같다
    /// </summary>
    public DateTime? arrivalDt { get; set; }

    /// <summary>
    /// 도착 예정의 근거. SLOT(배정 시각). 배정 시각이 없으면 null
    /// </summary>
    public string? arrivalSource { get; set; }

    /// <summary>
    /// 차 현재 위치. 차 위치 보고가 생기면 값이 있다. 지금은 늘 null
    /// </summary>
    public ChildPickupVehicleDto? vehicle { get; set; }

    /// <summary>
    /// 아이 출발 알림 상태
    /// </summary>
    public ChildPickupLeaveDto? leave { get; set; }
}

/// <summary>
/// 아이 픽업 차량 정보
/// </summary>
public class ChildPickupCarDto
{
    /// <summary>
    /// 차종
    /// </summary>
    public string? model { get; set; }

    /// <summary>
    /// 차 색(WHITE·BLACK·GRAY·ETC)
    /// </summary>
    public string? color { get; set; }

    /// <summary>
    /// 차량 번호 뒷자리 4자리
    /// </summary>
    public string? noLast4 { get; set; }
}

/// <summary>
/// 아이 픽업 차량 현재 위치
/// </summary>
public class ChildPickupVehicleDto
{
    /// <summary>
    /// 위도
    /// </summary>
    public decimal lat { get; set; }

    /// <summary>
    /// 경도
    /// </summary>
    public decimal lng { get; set; }

    /// <summary>
    /// 서버가 위치를 받은 시각
    /// </summary>
    public DateTime reportedDt { get; set; }
}

/// <summary>
/// 아이 출발 알림 상태
/// </summary>
public class ChildPickupLeaveDto
{
    /// <summary>
    /// 출발 상태 NONE·SCHEDULED·SOON·LEAVING
    /// </summary>
    public string leaveStatus { get; set; } = string.Empty;

    /// <summary>
    /// 출발 예정 시각(SCHEDULED·SOON일 때만)
    /// </summary>
    public DateTime? plannedLeaveDt { get; set; }

    /// <summary>
    /// "지금 나가요"를 누른 시각
    /// </summary>
    public DateTime? leavingDt { get; set; }
}
