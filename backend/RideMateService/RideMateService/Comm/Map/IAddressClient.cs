namespace RideMateService.Comm.Map;

/// <summary>
/// 좌표 → 장소 이름 변환 서비스
/// </summary>
public interface IAddressClient
{
    /// <summary>
    /// 좌표의 장소 이름을 조회한다. 도로명 주소의 건물 이름 → 도로명 주소 → 지번 주소 순으로 고르고 100자를 넘으면 자른다.
    /// 키가 없거나 HTTP 오류·타임아웃·결과 없음·응답 해석 실패이면 null을 돌려준다.
    /// 요청 취소만 예외로 올려보낸다.
    /// </summary>
    Task<string?> GetPlaceNameAsync(decimal lat, decimal lng, CancellationToken cancellationToken = default);
}
