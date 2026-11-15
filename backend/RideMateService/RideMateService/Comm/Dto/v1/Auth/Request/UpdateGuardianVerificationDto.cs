using System.ComponentModel.DataAnnotations;

namespace RideMateService.Comm.Dto.v1.Auth.Request;

/// <summary>
/// 보호자 인증번호 확인 요청
/// </summary>
public class UpdateGuardianVerificationDto
{
    /// <summary>
    /// 문자로 받은 6자리 인증번호
    /// </summary>
    [Required]
    [RegularExpression(@"^[0-9]{6}$")]
    public string? code { get; set; }
}
