using System.ComponentModel.DataAnnotations;

namespace RideMateService.Comm.Dto.v1.Pickup.Request;

/// <summary>
/// 단속 구역 목록 요청
/// </summary>
public class RestrictedZoneListQueryDto
{
    /// <summary>
    /// 앱이 보유한 단속 구역 목록 버전
    /// </summary>
    [StringLength(64)]
    public string? version { get; set; }
}
