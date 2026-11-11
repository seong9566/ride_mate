using System;
using System.Collections.Generic;

namespace RideMateService.Commons.DBModels;

/// <summary>
/// 운전자 계정
/// </summary>
public partial class UserInfo
{
    /// <summary>
    /// 운전자 계정 ID
    /// </summary>
    public int UserId { get; set; }

    /// <summary>
    /// 암호화한 휴대폰 번호
    /// </summary>
    public byte[] PhoneEnc { get; set; } = null!;

    /// <summary>
    /// 휴대폰 번호 HMAC
    /// </summary>
    public string PhoneHash { get; set; } = null!;

    /// <summary>
    /// 운전자 이름
    /// </summary>
    public string UserName { get; set; } = null!;

    /// <summary>
    /// 차종
    /// </summary>
    public string? CarModel { get; set; }

    /// <summary>
    /// 차 색
    /// </summary>
    public string? CarColor { get; set; }

    /// <summary>
    /// 차량 번호 뒷자리
    /// </summary>
    public string? CarNoLast4 { get; set; }

    /// <summary>
    /// 픽업 후 목적지 주소
    /// </summary>
    public string? DestAddress { get; set; }

    /// <summary>
    /// 픽업 후 목적지 위도
    /// </summary>
    public decimal? DestLat { get; set; }

    /// <summary>
    /// 픽업 후 목적지 경도
    /// </summary>
    public decimal? DestLng { get; set; }

    /// <summary>
    /// 링크 자동 발송 여부
    /// </summary>
    public bool AutoSendYn { get; set; }

    /// <summary>
    /// 차량 도착 기록 사용 여부
    /// </summary>
    public bool ArrivalLogYn { get; set; }

    /// <summary>
    /// 출발 전 깨우는 시간(분)
    /// </summary>
    public sbyte WakeBeforeMin { get; set; }

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

    public virtual ICollection<Child> Children { get; set; } = new List<Child>();

    public virtual ICollection<Consent> Consents { get; set; } = new List<Consent>();

    public virtual ICollection<LoginSession> LoginSessions { get; set; } = new List<LoginSession>();

    public virtual ICollection<PickupPlan> PickupPlans { get; set; } = new List<PickupPlan>();

    public virtual ICollection<QrLogin> QrLogins { get; set; } = new List<QrLogin>();
}
