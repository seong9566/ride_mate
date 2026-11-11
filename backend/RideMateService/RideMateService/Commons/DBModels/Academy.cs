using System;
using System.Collections.Generic;

namespace RideMateService.Commons.DBModels;

/// <summary>
/// 학원
/// </summary>
public partial class Academy
{
    /// <summary>
    /// 학원 ID
    /// </summary>
    public int AcademyId { get; set; }

    /// <summary>
    /// 학원가 ID
    /// </summary>
    public int? AreaId { get; set; }

    /// <summary>
    /// 학원 이름
    /// </summary>
    public string AcademyName { get; set; } = null!;

    /// <summary>
    /// 학원 주소
    /// </summary>
    public string Address { get; set; } = null!;

    /// <summary>
    /// 학원 위도
    /// </summary>
    public decimal? Lat { get; set; }

    /// <summary>
    /// 학원 경도
    /// </summary>
    public decimal? Lng { get; set; }

    /// <summary>
    /// 데이터 출처 유형 (TEAM, NEIS, PLACE)
    /// </summary>
    public string SourceType { get; set; } = null!;

    /// <summary>
    /// 장소 검색 API 장소 ID
    /// </summary>
    public string? PlaceId { get; set; }

    /// <summary>
    /// 원천 데이터 학원 관리번호
    /// </summary>
    public string? SourceRef { get; set; }

    /// <summary>
    /// 등록 상태 (OPEN, CLOSED)
    /// </summary>
    public string? OpenStatus { get; set; }

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

    public virtual ICollection<AcademyPickupSpot> AcademyPickupSpots { get; set; } = new List<AcademyPickupSpot>();

    public virtual Area? Area { get; set; }

    public virtual ICollection<PickupPlan> PickupPlans { get; set; } = new List<PickupPlan>();

    public virtual ICollection<Timetable> Timetables { get; set; } = new List<Timetable>();
}
