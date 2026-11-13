using System.ComponentModel.DataAnnotations;

namespace RideMateService.Comm.Dto.v1.ChildInfo.Request;

/// <summary>
/// 아이 정보 저장 요청
/// </summary>
public class AddChildDto
{
    /// <summary>
    /// 아이 이름. 앞뒤 공백을 지운 뒤 1~10자, 완성형 한글·영문만 쓰고 단어 사이 공백은 한 칸까지 허용한다(Service에서 검사)
    /// </summary>
    [Required]
    public string? childName { get; set; }

    /// <summary>
    /// 숫자로만 된 휴대폰 번호
    /// </summary>
    [Required]
    [RegularExpression(@"^01[016789][0-9]{7,8}$")]
    public string? phone { get; set; }

    /// <summary>
    /// 링크를 보내는 방법. SMS 또는 ALIMTALK
    /// </summary>
    [Required]
    [RegularExpression("^(SMS|ALIMTALK)$")]
    public string? sendChannel { get; set; }

    /// <summary>
    /// 생년월일. 화면 입력 그대로 yyyy.MM.dd (예: 2010.05.14). 없는 날짜나 오늘 이후 날짜는 400이다(Service에서 검사)
    /// </summary>
    [Required]
    [RegularExpression(@"^[0-9]{4}\.[0-9]{2}\.[0-9]{2}$")]
    public string? birthDate { get; set; }
}
