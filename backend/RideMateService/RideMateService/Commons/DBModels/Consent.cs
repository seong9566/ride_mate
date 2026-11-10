using System;
using System.Collections.Generic;

namespace RideMateService.Commons.DBModels;

/// <summary>
/// 동의 기록
/// </summary>
public partial class Consent
{
    /// <summary>
    /// 동의 기록 ID
    /// </summary>
    public int ConsentId { get; set; }

    /// <summary>
    /// 운전자 계정 ID
    /// </summary>
    public int UserId { get; set; }

    /// <summary>
    /// 동의 종류 (GUARDIAN, CHILD_INFO, ARRIVAL_LOG)
    /// </summary>
    public string ConsentType { get; set; } = null!;

    /// <summary>
    /// 동의 여부
    /// </summary>
    public bool AgreeYn { get; set; }

    /// <summary>
    /// 약관 및 고지 버전
    /// </summary>
    public string TermsVersion { get; set; } = null!;

    /// <summary>
    /// 확인 방법 (PHONE_OTP, CHECK)
    /// </summary>
    public string VerifyMethod { get; set; } = null!;

    /// <summary>
    /// 휴대폰 인증번호 ID
    /// </summary>
    public int? PhoneVerificationId { get; set; }

    /// <summary>
    /// 동의 일시
    /// </summary>
    public DateTime ConsentDt { get; set; }

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

    public virtual PhoneVerification? PhoneVerification { get; set; }

    public virtual UserInfo User { get; set; } = null!;
}
