using System.ComponentModel.DataAnnotations;

namespace RideMateService.Comm.Dto.v1.Auth.Request;

/// <summary>
/// 휴대폰 웹 QR 로그인 취소 요청
/// </summary>
public class DeleteWebQrLoginDto
{
    /// <summary>
    /// QR에 담긴 user_code 원문
    /// </summary>
    [Required]
    [StringLength(64, MinimumLength = 1)]
    public string? userCode { get; set; }
}
