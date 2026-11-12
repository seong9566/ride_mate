using System.ComponentModel.DataAnnotations;

namespace RideMateService.Comm.Dto.v1.User.Request;

/// <summary>
/// 자동 발송 설정 저장 요청
/// </summary>
public class UpdateSettingDto
{
    /// <summary>
    /// 자동 발송 여부. 빠지면 400
    /// </summary>
    [Required]
    public bool? autoSendYn { get; set; }
}
