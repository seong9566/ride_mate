namespace RideMateService.Service.v1.Pickup;

/// <summary>
/// 아이에게 보내는 픽업 문자 본문. 차량 앱의 미리보기와 같은 문구여야 한다
/// </summary>
public static class PickupLinkMessage
{
    /// <summary>
    /// 링크 문자
    /// </summary>
    public static string Link(string userName, DateTime etaDt, string spotName, string url)
    {
        return $"[라이드메이트] {userName} 차 {etaDt:HH:mm} 도착! {spotName}에서 만나\n{url}";
    }

    /// <summary>
    /// "도착했어요" 다시 알림 문자
    /// </summary>
    public static string Remind(string userName, string spotName, string url)
    {
        return $"[라이드메이트] {userName} 차가 {spotName}에 도착했어! 지금 나와\n{url}";
    }
}
