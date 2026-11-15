using System;
using System.Collections.Generic;

namespace RideMateService.Commons.DBModels;

/// <summary>
/// QR 로그인 요청
/// </summary>
public partial class QrLogin
{
    /// <summary>
    /// QR 로그인 요청 ID
    /// </summary>
    public int QrLoginId { get; set; }

    /// <summary>
    /// 차량이 보관하는 device_code HMAC
    /// </summary>
    public string DeviceCodeHash { get; set; } = null!;

    /// <summary>
    /// QR에 담긴 user_code HMAC
    /// </summary>
    public string UserCodeHash { get; set; } = null!;

    /// <summary>
    /// 차·휴대폰에 함께 보이는 숫자 4자리
    /// </summary>
    public string MatchCode { get; set; } = null!;

    /// <summary>
    /// 차량 앱 설치 번호
    /// </summary>
    public string DeviceId { get; set; } = null!;

    /// <summary>
    /// QR 로그인 상태 (PENDING, SCANNED, VERIFIED, APPROVED, USED, CANCELED)
    /// </summary>
    public string Status { get; set; } = null!;

    /// <summary>
    /// 취소 사유 (USER, DRIVING, PHONE)
    /// </summary>
    public string? CancelReason { get; set; }

    /// <summary>
    /// 인증한 운전자 계정 ID
    /// </summary>
    public int? UserId { get; set; }

    /// <summary>
    /// QR 로그인 만료 일시
    /// </summary>
    public DateTime ExpireDt { get; set; }

    /// <summary>
    /// 차량이 마지막으로 결과를 물은 일시
    /// </summary>
    public DateTime? LastPollDt { get; set; }

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

    public virtual UserInfo? User { get; set; }
}
