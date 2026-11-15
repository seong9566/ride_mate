namespace RideMateService.Comm.Map;

/// <summary>
/// 좌표 ↔ 주소 변환 서비스
/// </summary>
public interface IAddressClient
{
    /// <summary>
    /// 좌표의 장소 이름을 조회한다. 도로명 주소의 건물 이름 → 도로명 주소 → 지번 주소 순으로 고르고 100자를 넘으면 자른다.
    /// 키가 없거나 HTTP 오류·타임아웃·결과 없음·응답 해석 실패이면 null을 돌려준다.
    /// 요청 취소만 예외로 올려보낸다.
    /// </summary>
    Task<string?> GetPlaceNameAsync(decimal lat, decimal lng, CancellationToken cancellationToken = default);

    /// <summary>
    /// 주소를 좌표로 바꾼다. 결과가 여럿이면 첫 결과를 쓴다.
    /// 결과가 없으면 NotFound, 키가 없거나 HTTP 오류·타임아웃·응답 해석 실패이면 Failed를 돌려준다.
    /// 요청 취소만 예외로 올려보낸다.
    /// </summary>
    Task<AddressSearchResult> SearchAddressAsync(string address, CancellationToken cancellationToken = default);
}

/// <summary>
/// 주소 검색 결과 상태
/// </summary>
public enum AddressSearchStatus
{
    Found,
    NotFound,
    Failed
}

/// <summary>
/// 주소 검색 결과. Found일 때만 좌표가 있다
/// </summary>
public record AddressSearchResult(AddressSearchStatus Status, decimal? Lat = null, decimal? Lng = null);
