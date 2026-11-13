namespace RideMateService.Comm;

/// <summary>
/// 아이 휴대폰 웹(API와 다른 도메인)이 Pickup/W API를 부를 때 쓰는 CORS 정책.
/// 허용 오리진은 Cors:ChildWebOrigins 설정이고, 등록은 Program.cs의 CORS 블록에서 한다.
/// </summary>
public static class ChildWebCors
{
    /// <summary>
    /// 정책 이름
    /// </summary>
    public const string PolicyName = "ChildWeb";
}
