using System.Net;
using System.Net.Sockets;

namespace RideMateService.Comm;

/// <summary>
/// 로그인 전에 부르는 계정 목록 API의 요청 빈도 제한 값. 기기 번호만으로 운전자 이름이 보이므로 클라이언트마다 1분에 30번까지 받는다.
/// 등록은 Extensions/ServiceCollectionExtensions.cs의 AddRateLimitPolicies에서 한다.
/// </summary>
public static class LoginSessionListRateLimit
{
    /// <summary>
    /// 정책 이름
    /// </summary>
    public const string PolicyName = "LoginSessionList";

    /// <summary>
    /// 1분에 받는 요청 수
    /// </summary>
    public const int PermitLimit = 30;

    /// <summary>
    /// 제한을 나누는 키. IPv4는 주소 그대로, IPv6는 /64 접두로 묶는다(한 클라이언트가 /64를 통째로 쓰므로 주소를 바꿔 우회하지 못하게).
    /// IPv4-mapped IPv6는 IPv4로 바꾼다. 프록시 뒤에서는 UseForwardedHeaders가 복원한 주소를 쓴다.
    /// </summary>
    public static string PartitionKey(IPAddress? address)
    {
        if (address is null)
            return "unknown";

        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();

        if (address.AddressFamily != AddressFamily.InterNetworkV6)
            return address.ToString();

        var bytes = address.GetAddressBytes();
        Array.Clear(bytes, 8, 8);
        return new IPAddress(bytes) + "/64";
    }
}
