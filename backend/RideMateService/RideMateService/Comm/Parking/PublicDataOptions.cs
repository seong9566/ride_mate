namespace RideMateService.Comm.Parking;

/// <summary>
/// 공공데이터포털 API 설정. 설정 절 "PublicData".
/// </summary>
public class PublicDataOptions
{
    /// <summary>
    /// 공공데이터포털 서비스 키. 비어 있으면 API를 부르지 않는다. 비밀값이므로 환경변수 PublicData__ServiceKey로 주입한다.
    /// </summary>
    public string ServiceKey { get; set; } = string.Empty;
}
