namespace RideMateService.Comm.Dto.v1.ChildInfo.Response;

/// <summary>
/// 아이 정보 저장 결과. 이미 아이가 있어 409일 때는 먼저 저장된 아이 정보가 담긴다
/// </summary>
public class AddChildResultDto
{
    /// <summary>
    /// 아이 ID
    /// </summary>
    public int childSeq { get; set; }

    /// <summary>
    /// 저장한 아이 이름(앞뒤 공백을 지운 값)
    /// </summary>
    public string childName { get; set; } = string.Empty;

    /// <summary>
    /// 가운데를 가린 번호 (예: 010-****-5678). 409에서 저장된 번호를 복호화할 수 없으면 빈 문자열
    /// </summary>
    public string phoneMasked { get; set; } = string.Empty;

    /// <summary>
    /// 링크를 보내는 방법. SMS 또는 ALIMTALK
    /// </summary>
    public string sendChannel { get; set; } = string.Empty;
}
