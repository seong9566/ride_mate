using System;
using System.Collections.Generic;

namespace RideMateService.Commons.DBModels;

/// <summary>
/// 수집 대상 링크
/// </summary>
public partial class TrafficLink
{
    /// <summary>
    /// 수집 대상 링크 ID
    /// </summary>
    public int TrafficLinkId { get; set; }

    /// <summary>
    /// 소스 (TOPIS, GITS)
    /// </summary>
    public string Source { get; set; } = null!;

    /// <summary>
    /// 학원가 (DAECHI, PYEONGCHON)
    /// </summary>
    public string AreaCode { get; set; } = null!;

    /// <summary>
    /// 소스의 링크 ID (TOPIS와 GITS는 체계가 다름)
    /// </summary>
    public string LinkId { get; set; } = null!;

    /// <summary>
    /// GITS 도로 ID (도로 단위 수집용, TOPIS는 NULL)
    /// </summary>
    public string? RouteId { get; set; }

    /// <summary>
    /// 도로명
    /// </summary>
    public string RoadName { get; set; } = null!;

    /// <summary>
    /// 시작 지점명
    /// </summary>
    public string StNodeName { get; set; } = null!;

    /// <summary>
    /// 끝 지점명
    /// </summary>
    public string EdNodeName { get; set; } = null!;

    /// <summary>
    /// 방향 (TOPIS axis_dir, GITS routeWay)
    /// </summary>
    public string? AxisDir { get; set; }

    /// <summary>
    /// 링크 길이(m)
    /// </summary>
    public int DistanceM { get; set; }

    /// <summary>
    /// 학원가 중심에서 링크까지 최단 거리(m, TOPIS만 저장)
    /// </summary>
    public int? CenterDistM { get; set; }

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

    public virtual ICollection<TrafficSpeed> TrafficSpeeds { get; set; } = new List<TrafficSpeed>();
}
