using System;
using System.Collections.Generic;

namespace RideMateService.Commons.DBModels;

/// <summary>
/// 학원별 픽업 지점 후보
/// </summary>
public partial class AcademyPickupSpot
{
    /// <summary>
    /// 학원별 픽업 지점 후보 ID
    /// </summary>
    public int AcademyPickupSpotId { get; set; }

    /// <summary>
    /// 학원 ID
    /// </summary>
    public int AcademyId { get; set; }

    /// <summary>
    /// 픽업 지점 ID
    /// </summary>
    public int PickupSpotId { get; set; }

    /// <summary>
    /// 학원에서 지점까지 도보 시간(분)
    /// </summary>
    public sbyte WalkMin { get; set; }

    /// <summary>
    /// 배정 우선순위
    /// </summary>
    public sbyte Priority { get; set; }

    /// <summary>
    /// 학원 기준 지점 한 줄 안내
    /// </summary>
    public string? Note { get; set; }

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

    /// <summary>
    /// 살아 있는 행 표시 (UNIQUE용)
    /// </summary>
    public sbyte? ActiveFlag { get; set; }

    public virtual Academy Academy { get; set; } = null!;

    public virtual PickupSpot PickupSpot { get; set; } = null!;
}
