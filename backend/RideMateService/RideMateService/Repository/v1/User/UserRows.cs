namespace RideMateService.Repository.v1.User;

/// <summary>
/// 운전자 계정의 집·차·초기 설정 완료 시각
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
}
