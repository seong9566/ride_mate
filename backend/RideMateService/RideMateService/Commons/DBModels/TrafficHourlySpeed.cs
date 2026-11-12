using System;
using System.Collections.Generic;

namespace RideMateService.Commons.DBModels;

/// <summary>
/// TOPIS 월별 시간 속도
/// </summary>
public partial class TrafficHourlySpeed
{
    /// <summary>
    /// 월별 시간 속도 ID
    /// </summary>
    public int TrafficHourlySpeedId { get; set; }

    /// <summary>
    /// 수집 대상 링크 ID
    /// </summary>
    public int TrafficLinkId { get; set; }

    /// <summary>
    /// 속도 일자 (파일의 일자)
    /// </summary>
    public DateTime SpeedDate { get; set; }

    /// <summary>
    /// 시간 번호 (파일 열 ~HH시의 HH, 1~24)
    /// </summary>
    public sbyte HourNo { get; set; }

    /// <summary>
    /// 속도(km/h)
    /// </summary>
    public decimal SpeedKmh { get; set; }

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

    public virtual TrafficLink TrafficLink { get; set; } = null!;
}
