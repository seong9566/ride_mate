namespace RideMateService.Repository.v1.User;

/// <summary>
/// 운전자 계정의 집·차·초기 설정 완료 시각·자동 발송 여부
/// </summary>
public class UserSetupRow
{
    public string? DestAddress { get; set; }
    public decimal? DestLat { get; set; }
    public decimal? DestLng { get; set; }
    public string? CarModel { get; set; }
    public string? CarColor { get; set; }
    public string? CarNoLast4 { get; set; }
    public DateTime? SetupDoneDt { get; set; }
    public bool AutoSendYn { get; set; }
}

/// <summary>
/// 운전자 계정의 집·자동 발송 여부와 LEFT JOIN한 살아 있는 아이 한 명. 아이가 없으면 아이 칸은 모두 null이다
/// </summary>
public class UserDetailRow
{
    public string? DestAddress { get; set; }
    public decimal? DestLat { get; set; }
    public decimal? DestLng { get; set; }
    public bool AutoSendYn { get; set; }
    public int? ChildId { get; set; }
    public string? ChildName { get; set; }
    public byte[]? PhoneEnc { get; set; }
    public string? SendChannel { get; set; }
}
