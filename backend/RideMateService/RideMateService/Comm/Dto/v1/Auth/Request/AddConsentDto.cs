using System.ComponentModel.DataAnnotations;

namespace RideMateService.Comm.Dto.v1.Auth.Request;

/// <summary>
/// 보호자 동의 저장 요청
/// </summary>
public class AddConsentDto
{
    /// <summary>
    /// 보호자(법정대리인) 확인 동의. 필수라서 true만 받는다
    /// </summary>
    [Required]
    public bool? guardianYn { get; set; }

    /// <summary>
    /// 아이 정보 수집·이용 동의. 필수라서 true만 받는다
    /// </summary>
    [Required]
    public bool? childInfoYn { get; set; }

    /// <summary>
    /// 차량 도착 기록 동의(선택)
    /// </summary>
    [Required]
    public bool? arrivalLogYn { get; set; }
}
