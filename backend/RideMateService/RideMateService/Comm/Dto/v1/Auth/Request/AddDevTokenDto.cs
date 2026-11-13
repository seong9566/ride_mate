using System.ComponentModel.DataAnnotations;

namespace RideMateService.Comm.Dto.v1.Auth.Request;

/// <summary>
/// 개발 전용 토큰 발급 요청. Development 환경에서만 쓴다
/// </summary>
public class AddDevTokenDto
{
    /// <summary>
    /// 로그인할 계정 ID(USER_INFO.USER_ID)
    /// </summary>
    [Required]
    [Range(1, int.MaxValue)]
    public int? userId { get; set; }

    /// <summary>
    /// 차량 기기 ID. 비우면 swagger-dev. 같은 기기 ID의 이 계정 차량 세션은 끝난다
    /// </summary>
    [StringLength(100)]
    public string? device_id { get; set; }
}
