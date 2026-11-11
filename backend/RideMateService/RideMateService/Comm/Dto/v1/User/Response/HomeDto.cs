namespace RideMateService.Comm.Dto.v1.User.Response;

/// <summary>
/// 저장된 집 정보
/// </summary>
public class HomeDto
{
    /// <summary>
    /// 집 주소
    /// </summary>
    public string address { get; set; } = string.Empty;

    /// <summary>
    /// 집 위도. 좌표 없이 저장했으면 null
    /// </summary>
    public decimal? lat { get; set; }

    /// <summary>
    /// 집 경도. 좌표 없이 저장했으면 null
    /// </summary>
    public decimal? lng { get; set; }
}
