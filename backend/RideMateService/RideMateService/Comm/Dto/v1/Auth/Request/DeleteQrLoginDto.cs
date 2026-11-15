using System.ComponentModel.DataAnnotations;

namespace RideMateService.Comm.Dto.v1.Auth.Request;

/// <summary>
/// 차량 QR 로그인 취소 요청
/// </summary>
public class DeleteQrLoginDto
{
    /// <summary>
    /// 차량이 보관하는 device_code 원문
    /// </summary>
    [Required]
    [StringLength(128, MinimumLength = 1)]
    public string? deviceCode { get; set; }

    /// <summary>
    /// 차량 앱 설치 번호
    /// </summary>
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string? deviceId { get; set; }

    /// <summary>
    /// 취소 사유(USER, DRIVING)
    /// </summary>
    [Required]
    [RegularExpression("^(USER|DRIVING)$")]
    public string? reason { get; set; }
}
