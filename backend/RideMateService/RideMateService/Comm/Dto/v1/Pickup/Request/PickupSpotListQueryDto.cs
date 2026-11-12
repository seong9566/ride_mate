using System.ComponentModel.DataAnnotations;

namespace RideMateService.Comm.Dto.v1.Pickup.Request;

/// <summary>
/// 픽업 장소 후보 목록 요청
/// </summary>
public class PickupSpotListQueryDto : IValidatableObject
{
    /// <summary>
    /// 후보를 볼 시간표 ID. 시간표 목록의 scheduleSeq
    /// </summary>
    [Required]
    [Range(1, int.MaxValue)]
    public int? scheduleSeq { get; set; }

    /// <summary>
    /// 앱의 현재 시각(KST, 오프셋 없는 로컬 시각). 평가 날짜와 요일 유형을 정한다
    /// </summary>
    [Required]
    public DateTime? currentDt { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        => DepartureGuideQueryDto.ValidateCurrentDt(currentDt);
}
