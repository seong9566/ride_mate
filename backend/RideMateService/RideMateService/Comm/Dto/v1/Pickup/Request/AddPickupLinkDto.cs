using System.ComponentModel.DataAnnotations;

namespace RideMateService.Comm.Dto.v1.Pickup.Request;

/// <summary>
/// 픽업 링크 문자 발송 요청
/// </summary>
public class AddPickupLinkDto
{
    /// <summary>
    /// 오늘 계획 ID
    /// </summary>
    [Required]
    public int? pickupPlanSeq { get; set; }

    /// <summary>
    /// 차량 내비가 계산한 픽업 장소 도착 예정 시각(KST, 오프셋 없는 로컬 시각). 서버 현재 시각 기준 -10분 ~ +3시간
    /// </summary>
    [Required]
    public DateTime? etaDt { get; set; }

    /// <summary>
    /// 차량이 요청마다 만드는 UUID. 같은 발송을 다시 부를 때는 같은 값을 보낸다
    /// </summary>
    [Required]
    public Guid? clientEventId { get; set; }
}
