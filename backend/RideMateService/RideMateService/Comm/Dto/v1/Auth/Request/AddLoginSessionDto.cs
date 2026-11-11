using System.ComponentModel.DataAnnotations;

namespace RideMateService.Comm.Dto.v1.Auth.Request;

/// <summary>
/// 인증번호 로그인 요청
/// </summary>
public class AddLoginSessionDto
{
    /// <summary>
    /// 숫자로만 된 휴대폰 번호
    /// </summary>
    [Required]
    [RegularExpression(@"^01[016789][0-9]{7,8}$")]
    public string? phone { get; set; }

    /// <summary>
    /// 6자리 인증번호
    /// </summary>
    [Required]
    [RegularExpression(@"^[0-9]{6}$")]
    public string? code { get; set; }

    /// <summary>
    /// 앱이 처음 실행할 때 만든 설치 번호(UUID 등). 로그인마다 같은 값을 보낸다
    /// </summary>
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string? deviceId { get; set; }
}
