using System;
using System.Collections.Generic;

namespace RideMateService.Commons.DBModels;

/// <summary>
/// 시간표
/// </summary>
public partial class Timetable
{
    /// <summary>
    /// 시간표 ID
    /// </summary>
    public int TimetableId { get; set; }

    /// <summary>
    /// 아이 ID
    /// </summary>
    public int ChildId { get; set; }

    /// <summary>
    /// 학원 ID
    /// </summary>
    public int AcademyId { get; set; }

    /// <summary>
    /// 부모가 지정한 학원 이름
    /// </summary>
    public string DisplayName { get; set; } = null!;

    /// <summary>
    /// 하원 시각
    /// </summary>
    public TimeSpan EndTime { get; set; }

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

    public virtual Academy Academy { get; set; } = null!;

    public virtual Child Child { get; set; } = null!;

    public virtual ICollection<TimetableDay> TimetableDays { get; set; } = new List<TimetableDay>();
}
