namespace RideMateService.Comm.Dto.v1.Pickup.Response;

/// <summary>
/// 정차 판정 결과
/// </summary>
public class PickupStopCheckDto
{
    /// <summary>
    /// 배정 장소 반경(기본 30m) 안에 섰는지. 배정 장소가 없는 계획은 false(단, 그 정차를 같은 요청으로 다시 보낸 응답은 true로 나온다 — 받아들인 한계)
    /// </summary>
    public bool atSpot { get; set; }

    /// <summary>
    /// 배정 장소까지 직선 거리(m, 반올림). 배정 장소가 없거나 같은 요청을 다시 보낸 응답이면 null
    /// </summary>
    public int? distanceM { get; set; }

    /// <summary>
    /// 정차 위치의 단속 판정 (RESTRICTED, CLEAR, UNKNOWN). 출발 안내의 enforcement.status와 같은 규칙이다
    /// </summary>
    public string enforcement { get; set; } = string.Empty;

    /// <summary>
    /// 걸린 구역 유형. RESTRICTED가 아니면 빈 배열
    /// </summary>
    public List<string> zoneTypes { get; set; } = new();
}
