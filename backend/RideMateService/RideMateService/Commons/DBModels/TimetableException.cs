using System;
using System.Collections.Generic;

namespace RideMateService.Commons.DBModels;

/// <summary>
/// 날짜별 예외
/// </summary>
public partial class TimetableException
{
    /// <summary>
    /// 날짜별 예외 ID
    /// </summary>
    public int TimetableExceptionId { get; set; }

    /// <summary>
    /// 시간표 요일 ID
    /// </summary>
    public int TimetableDayId { get; set; }

    /// <summary>
    /// 예외 적용 날짜
    /// </summary>
    public DateTime TargetDate { get; set; }

    /// <summary>
    /// 예외 유형 (SKIP, TIME_CHANGE)
    /// </summary>
    public string ExceptionType { get; set; } = null!;

    /// <summary>
    /// 변경된 하원 시각
    /// </summary>
    public TimeSpan? ChangedEndTime { get; set; }

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

    public virtual TimetableDay TimetableDay { get; set; } = null!;
}
