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
    /// QR 토큰 HMAC
    /// </summary>
    public string QrTokenHash { get; set; } = null!;

    /// <summary>
    /// 차량 앱 설치 번호
    /// </summary>
    public string DeviceId { get; set; } = null!;

    /// <summary>
    /// QR 로그인 상태 (PENDING, APPROVED, USED)
    /// </summary>
    public string Status { get; set; } = null!;

    /// <summary>
    /// 인증된 부모 계정 ID
    /// </summary>
    public int? ParentId { get; set; }

    /// <summary>
    /// QR 로그인 만료 일시
    /// </summary>
    public DateTime ExpireDt { get; set; }

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

    public virtual Parent? Parent { get; set; }
}
