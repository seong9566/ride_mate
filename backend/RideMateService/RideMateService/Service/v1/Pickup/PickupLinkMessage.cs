using System.Globalization;

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
        return $"[라이드메이트] {userName} 차 {etaDt.ToString("HH:mm", CultureInfo.InvariantCulture)} 도착! {spotName}에서 만나\n{url}";
    }

    /// <summary>
    /// "도착했어요" 다시 알림 문자
    /// </summary>
    public static string Remind(string userName, string spotName, string url)
    {
        return $"[라이드메이트] {userName} 차가 {spotName}에 도착했어! 지금 나와\n{url}";
    }

    /// <summary>
    /// 부모 취소 문자. 링크는 넣지 않는다(끝난 링크다)
    /// </summary>
    public static string Cancel(string userName)
    {
        return $"[라이드메이트] 오늘 {userName} 차 픽업은 취소됐어. 부모님께 연락해 줘";
    }
}
