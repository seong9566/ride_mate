using System.ComponentModel.DataAnnotations;

namespace RideMateService.Comm.Dto.v1.User.Request;

/// <summary>
/// 차 정보 저장 요청. 빠지거나 null인 필드는 바꾸지 않는다
/// </summary>
public class UpdateVehicleDto
{
    /// <summary>
    /// 차종. 앞뒤 공백을 지운 뒤 1~20자(Service에서 검사)
    /// </summary>
    public string? carModel { get; set; }

    /// <summary>
    /// 차 색상. WHITE, BLACK, GRAY, ETC 중 하나
    /// </summary>
    [RegularExpression("^(WHITE|BLACK|GRAY|ETC)$")]
    public string? carColor { get; set; }

    /// <summary>
    /// 차량 번호 뒤 4자리. 숫자 4자리
    /// </summary>
    [RegularExpression("^[0-9]{4}$")]
    public string? carNoLast4 { get; set; }
}
