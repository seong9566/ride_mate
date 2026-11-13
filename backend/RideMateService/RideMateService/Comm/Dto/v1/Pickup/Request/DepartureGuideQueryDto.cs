using System.ComponentModel.DataAnnotations;

namespace RideMateService.Comm.Dto.v1.Pickup.Request;

/// <summary>
/// 출발 안내 계산 요청
/// </summary>
public class DepartureGuideQueryDto : IValidatableObject
{
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
    /// 내비로 구한 차량의 픽업 장소까지 소요 시간(분). 없으면 서버가 추정한다
    /// </summary>
    [Range(0, 300)]
    public int? driveMin { get; set; }

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
