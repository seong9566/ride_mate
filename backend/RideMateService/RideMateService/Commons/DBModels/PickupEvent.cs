using System;
using System.Collections.Generic;

namespace RideMateService.Commons.DBModels;

/// <summary>
/// 픽업 중 일어난 일
/// </summary>
public partial class PickupEvent
{
    /// <summary>
    /// 픽업 이벤트 ID
    /// </summary>
    public int PickupEventId { get; set; }

    /// <summary>
    /// 오늘 계획 ID
    /// </summary>
    public int PickupPlanId { get; set; }

    /// <summary>
    /// 이벤트 유형 (PLANNED, STARTED, WAIT_STARTED, END_TIME_CHANGED, REASSIGNED, LINK_SENT, LINK_RESENT, LINK_OPENED, CHILD_LEAVING, LEAVE_SCHEDULED, LEAVE_CANCELED, STOPPED, WRONG_STOP, REMIND_SENT, LOOP_STARTED, BOARDED, EXPIRED, CANCELED)
    /// </summary>
    public string EventType { get; set; } = null!;

    /// <summary>
    /// 이벤트 발생 일시
    /// </summary>
    public DateTime EventDt { get; set; }

    /// <summary>
    /// 차량 이벤트 UUID
    /// </summary>
    public Guid? ClientEventId { get; set; }

    /// <summary>
    /// 이벤트 유형별 부가 값
    /// </summary>
    public string? Detail { get; set; }

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

    public virtual PickupPlan PickupPlan { get; set; } = null!;
}
