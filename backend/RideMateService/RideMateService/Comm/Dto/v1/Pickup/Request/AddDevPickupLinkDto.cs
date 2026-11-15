using System.ComponentModel.DataAnnotations;

namespace RideMateService.Comm.Dto.v1.Pickup.Request;

/// <summary>
/// 개발 전용 픽업 링크 발급 요청
/// </summary>
public class AddDevPickupLinkDto
{
    /// <summary>
    /// 오늘 계획 ID. 경로 시작(UpdatePickupPlanStart)으로 만든 진행 중 계획
    /// </summary>
    [Required]
    public int? pickupPlanSeq { get; set; }
}
