namespace RideMateService.Comm.Dto.v1.User.Response;

/// <summary>
/// 계정 정보
/// </summary>
public class DetailUserDto
{
    /// <summary>
    /// 등록된 아이. 살아 있는 아이가 없으면 null
    /// </summary>
    public DetailUserChildDto? child { get; set; }

    /// <summary>
    /// 저장된 집. 집 주소가 없으면 null
    /// </summary>
    public HomeDto? home { get; set; }

    /// <summary>
    /// 자동 발송 여부
    /// </summary>
    public bool autoSendYn { get; set; }
}

/// <summary>
/// 계정에 등록된 아이
/// </summary>
public class DetailUserChildDto
{
    /// <summary>
    /// 아이 ID
    /// </summary>
    public int childSeq { get; set; }

    /// <summary>
    /// 아이 이름
    /// </summary>
    public string childName { get; set; } = string.Empty;

    /// <summary>
    /// 가린 휴대폰 번호 (010-****-5678). 복호화할 수 없으면 빈 문자열
    /// </summary>
    public string phoneMasked { get; set; } = string.Empty;

    /// <summary>
    /// 발송 채널 (SMS, ALIMTALK)
    /// </summary>
    public string sendChannel { get; set; } = string.Empty;
}
