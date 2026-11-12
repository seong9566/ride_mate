using System;
using System.Collections.Generic;

namespace RideMateService.Commons.DBModels;

/// <summary>
/// 픽업 지점
/// </summary>
public partial class PickupSpot
{
    /// <summary>
    /// 픽업 지점 ID
    /// </summary>
    public int PickupSpotId { get; set; }

    /// <summary>
    /// 학원가 ID
    /// </summary>
    public int AreaId { get; set; }

    /// <summary>
    /// 픽업 지점 이름
    /// </summary>
    public string SpotName { get; set; } = null!;

    /// <summary>
    /// 픽업 지점 위도
    /// </summary>
    public decimal Lat { get; set; }

    /// <summary>
    /// 픽업 지점 경도
    /// </summary>
    public decimal Lng { get; set; }

    /// <summary>
    /// 픽업 지점 찾는 법
    /// </summary>
    public string? GuideText { get; set; }

    /// <summary>
    /// 현장 사진 주소
    /// </summary>
    public string? PhotoUrl { get; set; }

    /// <summary>
    /// 동시 수용 차량 수
    /// </summary>
    public sbyte Capacity { get; set; }

    /// <summary>
    /// 배정 사용 여부
    /// </summary>
    public bool UseYn { get; set; }

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

    public virtual ICollection<AcademyPickupSpot> AcademyPickupSpots { get; set; } = new List<AcademyPickupSpot>();

    public virtual Area Area { get; set; } = null!;

    public virtual ICollection<LoopRoute> LoopRoutes { get; set; } = new List<LoopRoute>();

    public virtual ICollection<PickupPlan> PickupPlans { get; set; } = new List<PickupPlan>();

    public virtual ICollection<PickupSpotBlock> PickupSpotBlocks { get; set; } = new List<PickupSpotBlock>();

    public virtual ICollection<Timetable> Timetables { get; set; } = new List<Timetable>();
}
