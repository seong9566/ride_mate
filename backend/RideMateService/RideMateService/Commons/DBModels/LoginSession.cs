using System;
using System.Collections.Generic;

namespace RideMateService.Commons.DBModels;

/// <summary>
/// 차량 로그인
/// </summary>
public partial class LoginSession
{
    /// <summary>
    /// 차량 로그인 ID
    /// </summary>
    public int LoginSessionId { get; set; }

    /// <summary>
    /// 운전자 계정 ID
    /// </summary>
    public int UserId { get; set; }

    /// <summary>
    /// 로그인 토큰 HMAC
    /// </summary>
    public string TokenHash { get; set; } = null!;

    /// <summary>
    /// 차량 앱 설치 번호
    /// </summary>
    public string DeviceId { get; set; } = null!;

    /// <summary>
    /// 토큰 만료 일시
    /// </summary>
    public DateTime ExpireDt { get; set; }

    /// <summary>
    /// 마지막 사용 일시
    /// </summary>
    public DateTime LastUsedDt { get; set; }

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

    public virtual UserInfo User { get; set; } = null!;
}
