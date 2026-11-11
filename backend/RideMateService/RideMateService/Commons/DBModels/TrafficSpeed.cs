using System;
using System.Collections.Generic;

namespace RideMateService.Commons.DBModels;

/// <summary>
/// 5분 소통 이력
/// </summary>
public partial class TrafficSpeed
{
    /// <summary>
    /// 5분 소통 이력 ID
    /// </summary>
    public int TrafficSpeedId { get; set; }

    /// <summary>
    /// 수집 대상 링크 ID
    /// </summary>
    public int TrafficLinkId { get; set; }

    /// <summary>
    /// 수집 회차 ID
    /// </summary>
    public int CollectRunId { get; set; }

    /// <summary>
    /// 회차 시각(5분 경계, 한 회차의 행은 모두 같은 값)
    /// </summary>
    public DateTime CollectedDt { get; set; }

    /// <summary>
    /// 소스가 준 수집 시각 (GITS collDate, TOPIS는 NULL)
    /// </summary>
    public DateTime? SourceCollectedDt { get; set; }

    /// <summary>
    /// 속도 (TOPIS prcs_spd, GITS spd)
    /// </summary>
    public short SpeedKmh { get; set; }

    /// <summary>
    /// 여행시간 초 (TOPIS prcs_trv_time, GITS trvlTime)
    /// </summary>
    public int TravelSec { get; set; }

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

    public virtual TrafficCollectRun CollectRun { get; set; } = null!;

    public virtual TrafficLink TrafficLink { get; set; } = null!;
}
