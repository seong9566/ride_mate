using System;
using System.Collections.Generic;

namespace RideMateService.Commons.DBModels;

/// <summary>
/// 수집 회차
/// </summary>
public partial class TrafficCollectRun
{
    /// <summary>
    /// 수집 회차 ID
    /// </summary>
    public int CollectRunId { get; set; }

    /// <summary>
    /// 소스 (TOPIS, GITS)
    /// </summary>
    public string Source { get; set; } = null!;

    /// <summary>
    /// 회차 시각(KST, 5분 경계)
    /// </summary>
    public DateTime RunDt { get; set; }

    /// <summary>
    /// 수집 상태 (OK, PARTIAL, NO_KEY, FAILED)
    /// </summary>
    public string Status { get; set; } = null!;

    /// <summary>
    /// 대상 링크 수
    /// </summary>
    public int LinkCount { get; set; }

    /// <summary>
    /// 저장한 행 수
    /// </summary>
    public int SavedCount { get; set; }

    /// <summary>
    /// 실패 요약 (키·URL 제외)
    /// </summary>
    public string? ErrorMessage { get; set; }

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

    public virtual ICollection<TrafficSpeed> TrafficSpeeds { get; set; } = new List<TrafficSpeed>();
}
