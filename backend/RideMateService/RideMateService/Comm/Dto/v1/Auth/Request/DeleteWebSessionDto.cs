using System.ComponentModel.DataAnnotations;

namespace RideMateService.Comm.Dto.v1.Auth.Request;

/// <summary>
/// 휴대폰 웹 세션 종료 요청
/// </summary>
public class DeleteWebSessionDto
{
    /// <summary>
    /// 되돌릴 QR의 user_code 원문
    /// </summary>
    [StringLength(64)]
    public string? userCode { get; set; }
}
