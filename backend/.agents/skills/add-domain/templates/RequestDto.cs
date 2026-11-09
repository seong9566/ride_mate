using System.ComponentModel.DataAnnotations;

namespace RideMateService.Comm.Dto.v1.{도메인}.Request;

/// <summary>
/// {한글 설명}
/// </summary>
public class Add{도메인}Dto
{
    [Required]
    [StringLength(50)] // DB 컬럼 길이와 맞춘다
    public string name { get; set; } = string.Empty;

    public bool useYn { get; set; }
}
