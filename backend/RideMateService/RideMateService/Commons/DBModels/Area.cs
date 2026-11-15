using System;
using System.Collections.Generic;

namespace RideMateService.Commons.DBModels;

/// <summary>
/// 학원가(서비스 지역)
/// </summary>
public partial class Area
{
    /// <summary>
    /// 학원가(서비스 지역) ID
    /// </summary>
    public int AreaId { get; set; }

    /// <summary>
    /// 학원가 이름
    /// </summary>
    public string AreaName { get; set; } = null!;

    /// <summary>
    /// 학원가 코드 (DAECHI, PYEONGCHON)
    /// </summary>
    public string? AreaCode { get; set; }

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

    public virtual ICollection<Academy> Academies { get; set; } = new List<Academy>();

    public virtual ICollection<ParkingLot> ParkingLots { get; set; } = new List<ParkingLot>();

    public virtual ICollection<PickupPlan> PickupPlans { get; set; } = new List<PickupPlan>();

    public virtual ICollection<PickupSpotRequest> PickupSpotRequests { get; set; } = new List<PickupSpotRequest>();

    public virtual ICollection<PickupSpot> PickupSpots { get; set; } = new List<PickupSpot>();

    public virtual ICollection<RestrictedZone> RestrictedZones { get; set; } = new List<RestrictedZone>();
}
