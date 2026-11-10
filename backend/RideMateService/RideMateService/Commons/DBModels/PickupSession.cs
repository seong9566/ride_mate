using System;
using System.Collections.Generic;

namespace RideMateService.Commons.DBModels;

/// <summary>
/// 픽업 세션(아이에게 보낸 링크)
/// </summary>
public partial class PickupSession
{
    /// <summary>
    /// 픽업 세션 ID
    /// </summary>
    public int PickupSessionId { get; set; }

    /// <summary>
    /// 오늘 계획 ID
    /// </summary>
    public int PickupPlanId { get; set; }

    /// <summary>
    /// 토큰 생성 난수
    /// </summary>
    public string TokenNonce { get; set; } = null!;

    /// <summary>
    /// 링크 토큰 HMAC
    /// </summary>
    public string TokenHash { get; set; } = null!;

    /// <summary>
    /// 발송 채널 (SMS, ALIMTALK)
    /// </summary>
    public string SendChannel { get; set; } = null!;

    /// <summary>
    /// 링크 종료 일시
    /// </summary>
    public DateTime? CloseDt { get; set; }

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
