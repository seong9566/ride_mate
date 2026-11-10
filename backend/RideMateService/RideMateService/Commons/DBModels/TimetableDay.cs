using System;
using System.Collections.Generic;

namespace RideMateService.Commons.DBModels;

/// <summary>
/// 시간표의 요일
/// </summary>
public partial class TimetableDay
{
    /// <summary>
    /// 시간표 요일 ID
    /// </summary>
    public int TimetableDayId { get; set; }

    /// <summary>
    /// 시간표 ID
    /// </summary>
    public int TimetableId { get; set; }

    /// <summary>
    /// 요일 (1=월, 2=화, 3=수, 4=목, 5=금, 6=토, 7=일)
    /// </summary>
    public sbyte DayOfWeek { get; set; }

    /// <summary>
    /// 요일별 픽업 여부
    /// </summary>
    public bool PickupYn { get; set; }

    /// <summary>
    /// 마지막 완료 픽업 날짜
    /// </summary>
    public DateTime? LastPickupDate { get; set; }

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

    public virtual ICollection<PickupPlan> PickupPlans { get; set; } = new List<PickupPlan>();

    public virtual Timetable Timetable { get; set; } = null!;

    public virtual ICollection<TimetableException> TimetableExceptions { get; set; } = new List<TimetableException>();
}
