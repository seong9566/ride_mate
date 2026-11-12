using System;
using System.Collections.Generic;

namespace RideMateService.Commons.DBModels;

/// <summary>
/// 픽업 장소 추가 요청
/// </summary>
public partial class PickupSpotRequest
{
    /// <summary>
    /// 픽업 장소 추가 요청 ID
    /// </summary>
    public int PickupSpotRequestId { get; set; }

    /// <summary>
    /// 학원 ID
    /// </summary>
    public int AcademyId { get; set; }

    /// <summary>
    /// 학원가 ID. 학원에 좌표가 없으면 NULL
    /// </summary>
    public int? AreaId { get; set; }

    /// <summary>
    /// 원하는 위치 위도
    /// </summary>
    public decimal? Lat { get; set; }

    /// <summary>
    /// 원하는 위치 경도
    /// </summary>
    public decimal? Lng { get; set; }

    /// <summary>
    /// 원하는 장소 이름
    /// </summary>
    public string? PlaceName { get; set; }

    /// <summary>
    /// 요청 메모
    /// </summary>
    public string? Memo { get; set; }

    /// <summary>
    /// 처리 상태 (REQUESTED, ACCEPTED, REJECTED)
    /// </summary>
    public string Status { get; set; } = null!;

    /// <summary>
    /// 생성 일시
    /// </summary>
    public DateTime CreateDt { get; set; }

    /// <summary>
    /// 생성자
    /// </summary>
    public string CreateUser { get; set; } = null!;

    /// <summary>
    /// 수정 일시
    /// </summary>
    public DateTime UpdateDt { get; set; }

    /// <summary>
    /// 수정자
    /// </summary>
    public string UpdateUser { get; set; } = null!;

    /// <summary>
    /// 삭제 여부
    /// </summary>
    public bool DeleteYn { get; set; }

    public virtual Academy Academy { get; set; } = null!;

    public virtual Area? Area { get; set; }
}
