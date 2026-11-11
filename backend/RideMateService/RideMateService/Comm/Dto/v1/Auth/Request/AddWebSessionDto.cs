using System.ComponentModel.DataAnnotations;

namespace RideMateService.Comm.Dto.v1.Auth.Request;

/// <summary>
/// 휴대폰 웹 인증번호 확인·가입 요청
/// </summary>
public class AddWebSessionDto
{
    /// <summary>
    /// 숫자로만 된 휴대폰 번호
    /// </summary>
    [Required]
    [RegularExpression(@"^01[016789][0-9]{7,8}$")]
    public string? phone { get; set; }

    /// <summary>
    /// 숫자 6자리 인증번호
    /// </summary>
    [Required]
    [RegularExpression(@"^[0-9]{6}$")]
    public string? code { get; set; }

    /// <summary>
    /// QR에 담긴 user_code 원문
    /// </summary>
    [Required]
    [StringLength(64, MinimumLength = 1)]
    public string? userCode { get; set; }

    /// <summary>
    /// 처음 가입할 때 저장할 운전자 이름
    /// </summary>
    [StringLength(50)]
    public string? userName { get; set; }

    /// <summary>
    /// 서비스 이용약관 동의 여부
    /// </summary>
    public bool? agreeTerms { get; set; }

    /// <summary>
    /// 개인정보 수집·이용 동의 여부
    /// </summary>
    public bool? agreePrivacy { get; set; }
}
