using System;
using System.Collections.Generic;

namespace RideMateService.Commons.DBModels;

/// <summary>
/// 아이
/// </summary>
public partial class Child
{
    /// <summary>
    /// 아이 ID
    /// </summary>
    public int ChildId { get; set; }

    /// <summary>
    /// 부모 계정 ID
    /// </summary>
    public int ParentId { get; set; }

    /// <summary>
    /// 아이 이름
    /// </summary>
    public string ChildName { get; set; } = null!;

    /// <summary>
    /// 암호화한 휴대폰 번호
    /// </summary>
    public byte[] PhoneEnc { get; set; } = null!;

    /// <summary>
    /// 발송 채널 (SMS, ALIMTALK)
    /// </summary>
    public string SendChannel { get; set; } = null!;

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

    public virtual Parent Parent { get; set; } = null!;

    public virtual ICollection<PickupPlan> PickupPlans { get; set; } = new List<PickupPlan>();

    public virtual ICollection<Timetable> Timetables { get; set; } = new List<Timetable>();
}
