namespace RideMateService.Comm.Dto.v1.Pickup.Response;

/// <summary>아이 픽업 안내 전체 스냅샷 이벤트</summary>
public class ChildPickupSnapshotEventDto : ChildPickupDto
{
    /// <summary>
    /// 직전 스냅샷과의 차이. PLACE_CHANGED(픽업 장소·배정 시각, 웹은 "픽업 장소가 바뀌었어요" 표시),
    /// STATUS_CHANGED(계획 상태·정차 위치·링크 상태), LEAVE_CHANGED(출발 알림), OTHER. 첫 스냅샷은 null
    /// </summary>
    public string? change { get; set; }
}

/// <summary>차량 위치와 도착 예정 시각 이벤트. 위치가 신선하지 않으면 위치 필드는 null이고 도착 근거는 SLOT</summary>
public class ChildPickupVehicleEventDto
{
    /// <summary>차 위도. 신선한 위치가 없으면 null</summary>
    public decimal? lat { get; set; }

    /// <summary>차 경도. 신선한 위치가 없으면 null</summary>
    public decimal? lng { get; set; }

    /// <summary>서버 위치 수신 시각. 신선한 위치가 없으면 null</summary>
    public DateTime? reportedDt { get; set; }

    /// <summary>차 도착 예정 시각</summary>
    public DateTime? arrivalDt { get; set; }

    /// <summary>도착 예정 근거 NAVI 또는 SLOT</summary>
    public string? arrivalSource { get; set; }

    /// <summary>서버 현재 시각(KST)</summary>
    public DateTime serverDt { get; set; }
}

/// <summary>서비스 내부에서 사용하는 스트림 연결 판정 결과</summary>
/// <param name="PickupPlanId">픽업 계획 ID</param>
/// <param name="TokenHash">링크 토큰 해시</param>
/// <param name="Snapshot">연결 직후 전달할 스냅샷</param>
/// <param name="ExpireDt">링크 만료 시각</param>
public sealed record ChildPickupStreamTicket(int PickupPlanId, string TokenHash, ChildPickupDto Snapshot, DateTime? ExpireDt);

/// <summary>아이 픽업 SSE 이벤트</summary>
/// <param name="EventName">이벤트 이름. null이면 하트비트 주석 줄</param>
/// <param name="Data">이벤트 데이터</param>
public sealed record ChildPickupStreamEvent(string? EventName, object? Data);
