using System.ComponentModel.DataAnnotations;

namespace RideMateService.Comm.Dto.v1.Pickup.Request;

/// <summary>
/// 출발 안내 계산 요청
/// </summary>
public class DepartureGuideQueryDto : IValidatableObject
{
    /// <summary>
    /// 앱의 현재 시각(KST, 오프셋 없는 로컬 시각)
    /// </summary>
    [Required]
    public DateTime? currentDt { get; set; }

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

    /// <summary>
    /// currentDt 검증. 픽업 장소 후보 목록 요청도 같은 규칙을 쓴다
    /// </summary>
    public static IEnumerable<ValidationResult> ValidateCurrentDt(DateTime? currentDt)
    {
        // 쿼리 바인딩은 Z·오프셋이 붙은 값을 UTC로 바꿔 시각이 밀린다. 오프셋 없는 로컬 시각(Unspecified)만 받는다
        if (currentDt is { Kind: not DateTimeKind.Unspecified })
            yield return new ValidationResult("currentDt는 오프셋 없는 로컬 시각이어야 합니다.",
                new[] { nameof(currentDt) });
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        foreach (var result in ValidateCurrentDt(currentDt))
            yield return result;

        if ((pickupLat is null) != (pickupLng is null))
            yield return new ValidationResult("pickupLat와 pickupLng는 함께 보내야 합니다.",
                new[] { nameof(pickupLat), nameof(pickupLng) });
    }
}
