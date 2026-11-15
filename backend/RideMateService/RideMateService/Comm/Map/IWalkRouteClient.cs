namespace RideMateService.Comm.Map;

/// <summary>
/// 도보 경로 조회 서비스
/// </summary>
public interface IWalkRouteClient
{
    /// <summary>
    /// 출발지에서 도착지까지의 도보 경로를 조회한다.
    /// 키가 없거나 HTTP 오류·타임아웃·응답 상태 이상·응답 해석 실패이면 null을 돌려준다.
    /// 요청 취소만 예외로 올려보낸다.
    /// </summary>
    Task<WalkRouteResult?> GetWalkRouteAsync(decimal startLat, decimal startLng, decimal endLat, decimal endLng, CancellationToken cancellationToken = default);
}

/// <summary>
/// 도보 경로 조회 결과
/// </summary>
public class WalkRouteResult
{
    /// <summary>
    /// 총 소요 시간(초)
    /// </summary>
    public int TotalSeconds { get; set; }

    /// <summary>
    /// 경로 선을 이루는 점. 출발지에서 도착지 순서이며 연속된 중복 점은 없다.
    /// </summary>
    public List<(decimal Lat, decimal Lng)> Path { get; set; } = new();
}
