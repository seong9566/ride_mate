using System.ComponentModel.DataAnnotations;

namespace RideMateService.Comm.Dto.v1.Pickup.Request;

/// <summary>
/// 출발 안내 계산 요청
/// </summary>
public class DepartureGuideQueryDto : IValidatableObject
{
    /// <summary>
    /// 고른 시간표 ID(이번 주 스케줄의 scheduleSeq)
    /// </summary>
    [Required]
    [Range(1, int.MaxValue)]
    public int? scheduleSeq { get; set; }

    /// <summary>
    /// 고른 요일. 1(월)~7(일). 서버의 오늘 요일과 다르면 code 101이다
    /// </summary>
    [Required]
    [Range(1, 7)]
    public int? dayOfWeek { get; set; }

    /// <summary>
    /// 오늘 고른 후보 지점. 픽업 좌표와 함께 보낼 수 없다
    /// </summary>
    [Range(1, int.MaxValue)]
    public int? pickupSpotSeq { get; set; }

    /// <summary>
    /// 차량 현재 위도
    /// </summary>
    [Required]
    [Range(-90.0, 90.0)]
    public decimal? currentLat { get; set; }

    /// <summary>
    /// 차량 현재 경도
    /// </summary>
    [Required]
    [Range(-180.0, 180.0)]
    public decimal? currentLng { get; set; }

    /// <summary>
    /// 부모가 바꾼 픽업 장소 위도. pickupLng와 함께 보낸다
    /// </summary>
    [Range(-90.0, 90.0)]
    public decimal? pickupLat { get; set; }

    /// <summary>
    /// 부모가 바꾼 픽업 장소 경도. pickupLat와 함께 보낸다
    /// </summary>
    [Range(-180.0, 180.0)]
    public decimal? pickupLng { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (pickupSpotSeq is not null && (pickupLat is not null || pickupLng is not null))
            yield return new ValidationResult("pickupSpotSeq와 픽업 좌표는 함께 보낼 수 없습니다.",
                new[] { nameof(pickupSpotSeq), nameof(pickupLat), nameof(pickupLng) });

        if ((pickupLat is null) != (pickupLng is null))
            yield return new ValidationResult("pickupLat와 pickupLng는 함께 보내야 합니다.",
                new[] { nameof(pickupLat), nameof(pickupLng) });
    }
}
