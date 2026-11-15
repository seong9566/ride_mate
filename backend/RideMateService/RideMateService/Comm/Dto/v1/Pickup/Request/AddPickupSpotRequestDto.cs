using System.ComponentModel.DataAnnotations;

namespace RideMateService.Comm.Dto.v1.Pickup.Request;

/// <summary>
/// 픽업 장소 추가 요청. 위치·이름·메모는 모두 선택이다
/// </summary>
public class AddPickupSpotRequestDto
{
    /// <summary>
    /// 요청하는 학원의 시간표 ID. 시간표 목록의 scheduleSeq
    /// </summary>
    [Required]
    [Range(1, int.MaxValue)]
    public int? scheduleSeq { get; set; }

    /// <summary>
    /// 원하는 위치 위도. lng와 함께 보낸다
    /// </summary>
    [Range(-90.0, 90.0)]
    public decimal? lat { get; set; }

    /// <summary>
    /// 원하는 위치 경도. lat와 함께 보낸다
    /// </summary>
    [Range(-180.0, 180.0)]
    public decimal? lng { get; set; }

    /// <summary>
    /// 원하는 장소 이름. 앞뒤 공백을 지운 뒤 100자 이하, 빈 문자열이면 저장하지 않는다
    /// </summary>
    [TrimmedMaxLength(100)]
    public string? placeName { get; set; }

    /// <summary>
    /// 요청 메모. 앞뒤 공백을 지운 뒤 200자 이하, 빈 문자열이면 저장하지 않는다
    /// </summary>
    [TrimmedMaxLength(200)]
    public string? memo { get; set; }
}
