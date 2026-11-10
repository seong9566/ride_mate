using System;
using System.Collections.Generic;

namespace RideMateService.Commons.DBModels;

/// <summary>
/// 휴대폰 인증번호
/// </summary>
public partial class PhoneVerification
{
    /// <summary>
    /// 휴대폰 인증번호 ID
    /// </summary>
    public int PhoneVerificationId { get; set; }

    /// <summary>
    /// 발송 용도 (LOGIN, GUARDIAN)
    /// </summary>
    public string Purpose { get; set; } = null!;

    /// <summary>
    /// 수신 번호 HMAC
    /// </summary>
    public string PhoneHash { get; set; } = null!;

    /// <summary>
    /// 인증번호 HMAC
    /// </summary>
    public string CodeHash { get; set; } = null!;

    /// <summary>
    /// 인증번호 만료 일시
    /// </summary>
    public DateTime ExpireDt { get; set; }

    /// <summary>
    /// 인증 실패 횟수
    /// </summary>
    public sbyte FailCnt { get; set; }

    /// <summary>
    /// 잠금 해제 일시
    /// </summary>
    public DateTime? LockUntilDt { get; set; }

    /// <summary>
    /// 인증 성공 일시
    /// </summary>
    public DateTime? VerifyDt { get; set; }

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

    public virtual ICollection<Consent> Consents { get; set; } = new List<Consent>();
}
