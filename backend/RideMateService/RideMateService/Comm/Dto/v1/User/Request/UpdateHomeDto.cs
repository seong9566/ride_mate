using System.ComponentModel.DataAnnotations;

namespace RideMateService.Comm.Dto.v1.User.Request;

/// <summary>
/// 집 저장 요청
/// </summary>
public class UpdateHomeDto
{
    /// <summary>
    /// 집 주소. 앞뒤 공백을 지운 뒤 1~200자(Service에서 검사)
    /// </summary>
    [Required]
    public string? address { get; set; }

    /// <summary>
    /// 위도. 경도와 함께 보내거나 둘 다 뺀다. 빼면 저장된 좌표가 지워진다
    /// </summary>
    [Range(-90.0, 90.0)]
    public decimal? lat { get; set; }

    /// <summary>
    /// 경도. 위도와 함께 보내거나 둘 다 뺀다. 빼면 저장된 좌표가 지워진다
    /// </summary>
    [Range(-180.0, 180.0)]
    public decimal? lng { get; set; }
}
