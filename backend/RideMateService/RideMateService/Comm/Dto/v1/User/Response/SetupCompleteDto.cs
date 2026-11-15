namespace RideMateService.Comm.Dto.v1.User.Response;

/// <summary>
/// 초기 설정 완료 결과
/// </summary>
public class SetupCompleteDto
{
    /// <summary>
    /// 초기 설정을 완료한 시각. 오프셋 없는 KST. 이미 완료한 계정이 다시 불러도 처음 시각 그대로다
    /// </summary>
    public DateTime setupDoneDt { get; set; }
}
