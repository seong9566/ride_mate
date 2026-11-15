namespace RideMateService.Comm.Parking;

/// <summary>
/// 공영주차장 실시간 빈자리 조회 서비스
/// </summary>
public interface IParkingRealtimeClient
{
    /// <summary>
    /// 요청한 실시간 주차장 코드별 빈자리 수를 조회한다.
    /// 키가 없거나 한 지역이라도 실패·타임아웃이면 null을 돌려주며, 요청 취소만 예외로 올려보낸다.
    /// </summary>
    Task<IReadOnlyDictionary<string, int>?> GetAvailableAsync(IReadOnlyCollection<string> realtimeRefs,
        CancellationToken cancellationToken = default);
}
