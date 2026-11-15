namespace RideMateService.Comm.Dto.v1.User.Response;

/// <summary>
/// 저장된 차 정보. 입력한 적 없는 값은 null
/// </summary>
public class VehicleDto
{
    /// <summary>
    /// 차종
    /// </summary>
    public string? carModel { get; set; }

    /// <summary>
    /// 차 색상. WHITE, BLACK, GRAY, ETC 중 하나
    /// </summary>
    public string? carColor { get; set; }

    /// <summary>
    /// 차량 번호 뒤 4자리
    /// </summary>
    public string? carNoLast4 { get; set; }
}
