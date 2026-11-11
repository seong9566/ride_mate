using System.ComponentModel.DataAnnotations;

namespace RideMateService.Comm.Dto.v1.Auth.Request;

/// <summary>
/// 토큰 갱신 요청
/// </summary>
public class UpdateLoginSessionDto
{
    /// <summary>
    /// refresh 토큰 원문
    /// </summary>
    [Required]
    [StringLength(128, MinimumLength = 1)]
    public string? refreshToken { get; set; }
}
