namespace RideMateService.Comm.Map;

/// <summary>
/// 자동차 경로 조회 서비스
/// </summary>
public interface IDriveRouteClient
{
    /// <summary>
    /// 출발지에서 경유지를 순서대로 거쳐 도착지까지 지금 출발 기준 자동차 경로를 조회한다. 구간은 이 메서드에 넘긴 경유지 수 + 1개다.
    /// 키가 없거나 HTTP 오류·타임아웃·결과 코드 이상·응답 해석 실패이면 null을 돌려준다.
    /// 요청 취소만 예외로 올려보낸다.
    /// </summary>
    Task<DriveRouteResult?> GetDriveRouteAsync((decimal Lat, decimal Lng) start,
        IReadOnlyList<(decimal Lat, decimal Lng)> waypoints, (decimal Lat, decimal Lng) end,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 자동차 경로 조회 결과
/// </summary>
public class DriveRouteResult
{
    /// <summary>
    /// 총 거리(m)
    /// </summary>
    public int DistanceM { get; set; }

    /// <summary>
    /// 구간별 결과. 출발지 → 경유지1 → … → 도착지 순서다
    /// </summary>
    public List<DriveRouteLeg> Legs { get; set; } = new();
}

/// <summary>
/// 자동차 경로의 한 구간
/// </summary>
public class DriveRouteLeg
{
    /// <summary>
    /// 소요 시간(초)
    /// </summary>
    public int Seconds { get; set; }

    /// <summary>
    /// 거리(m)
    /// </summary>
    public int DistanceM { get; set; }
}
