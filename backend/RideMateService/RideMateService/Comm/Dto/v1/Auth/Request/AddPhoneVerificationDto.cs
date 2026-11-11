using System.ComponentModel.DataAnnotations;

namespace RideMateService.Comm.Dto.v1.Auth.Request;

/// <summary>
/// 로그인 인증번호 발송 요청
/// </summary>
public class AddPhoneVerificationDto
{
    /// <summary>
    /// 숫자로만 된 휴대폰 번호
    /// </summary>
    [Required]
    [RegularExpression(@"^01[016789][0-9]{7,8}$")]
    public string? phone { get; set; }
}
