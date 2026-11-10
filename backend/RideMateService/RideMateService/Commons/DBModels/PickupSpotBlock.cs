using System;
using System.Collections.Generic;

namespace RideMateService.Commons.DBModels;

/// <summary>
/// 픽업 지점을 쓸 수 없는 시간
/// </summary>
public partial class PickupSpotBlock
{
    /// <summary>
    /// 픽업 지점 금지 시간 ID
    /// </summary>
    public int PickupSpotBlockId { get; set; }

    /// <summary>
    /// 픽업 지점 ID
    /// </summary>
    public int PickupSpotId { get; set; }

    /// <summary>
    /// 적용 요일 유형 (WEEKDAY, SAT, HOLIDAY)
    /// </summary>
    public string DayType { get; set; } = null!;

    /// <summary>
    /// 금지 시작 시각
    /// </summary>
    public TimeSpan StartTime { get; set; }

    /// <summary>
    /// 금지 종료 시각
    /// </summary>
    public TimeSpan EndTime { get; set; }

    /// <summary>
    /// 금지 사유
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// 원천 데이터 관리번호
    /// </summary>
    public string? SourceRef { get; set; }

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

    public virtual PickupSpot PickupSpot { get; set; } = null!;
}
