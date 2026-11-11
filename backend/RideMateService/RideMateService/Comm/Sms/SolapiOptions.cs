namespace RideMateService.Comm.Sms;

/// <summary>
/// 솔라피 문자 발송 설정. 설정 절 "Sms:Solapi". 비밀값이므로 환경변수(Sms__Solapi__ApiKey 등)나 git 제외 설정 파일로 주입한다.
/// </summary>
public class SolapiOptions
{
    /// <summary>
    /// 솔라피 API Key
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// 솔라피 API Secret
    /// </summary>
    public string ApiSecret { get; set; } = string.Empty;

    /// <summary>
    /// 솔라피에 등록한 발신번호. 숫자만 쓴다.
    /// </summary>
    public string Sender { get; set; } = string.Empty;
}
