using System.Security.Cryptography;
using System.Threading.RateLimiting;
using Microsoft.Extensions.Options;
using RideMateService.Comm;
using RideMateService.Comm.Logger;
using RideMateService.Comm.Map;
using RideMateService.Comm.Parking;
using RideMateService.Comm.Security;
using RideMateService.Comm.Sms;
using RideMateService.Repository.v1.Auth;
using RideMateService.Repository.v1.ChildInfo;
using RideMateService.Repository.v1.Pickup;
using RideMateService.Repository.v1.Schedule;
using RideMateService.Repository.v1.User;
using RideMateService.Service.v1.Auth;
using RideMateService.Service.v1.ChildInfo;
using RideMateService.Service.v1.Pickup;
using RideMateService.Service.v1.Schedule;
using RideMateService.Service.v1.User;

namespace RideMateService.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 도메인 Repository / Service 일괄 등록
    /// </summary>
    public static IServiceCollection AddDomainModules(this IServiceCollection services)
    {
        services.AddAuthModule();
        services.AddPickupModule();
        services.AddChildInfoModule();
        services.AddScheduleModule();
        services.AddUserModule();
        // 도메인을 추가하면 여기에 한 줄
        return services;
    }

    private static IServiceCollection AddAuthModule(this IServiceCollection services)
    {
        services.AddTransient<IAuthRepository, AuthRepository>();
        services.AddTransient<IAuthService, AuthService>();
        return services;
    }

    private static IServiceCollection AddPickupModule(this IServiceCollection services)
    {
        services.AddTransient<IPickupRepository, PickupRepository>();
        services.AddTransient<IPickupService, PickupService>();
        return services;
    }

    private static IServiceCollection AddChildInfoModule(this IServiceCollection services)
    {
        services.AddTransient<IChildInfoRepository, ChildInfoRepository>();
        services.AddTransient<IChildInfoService, ChildInfoService>();
        return services;
    }

    private static IServiceCollection AddScheduleModule(this IServiceCollection services)
    {
        services.AddTransient<IScheduleRepository, ScheduleRepository>();
        services.AddTransient<IScheduleService, ScheduleService>();
        return services;
    }

    private static IServiceCollection AddUserModule(this IServiceCollection services)
    {
        services.AddTransient<IUserRepository, UserRepository>();
        services.AddTransient<IUserService, UserService>();
        return services;
    }
}

public static class InfrastructureExtensions
{
    /// <summary>
    /// 요청 빈도 제한 정책 등록. 차 계정 목록(로그인 전 조회)만 클라이언트별 고정 창 30회/분으로 막고, 넘으면 본문 없이 429와 Retry-After를 돌려준다.
    /// </summary>
    public static IServiceCollection AddRateLimitPolicies(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = (context, _) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    context.HttpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();
                return ValueTask.CompletedTask;
            };
            options.AddPolicy(LoginSessionListRateLimit.PolicyName, context => RateLimitPartition.GetFixedWindowLimiter(
                LoginSessionListRateLimit.PartitionKey(context.Connection.RemoteIpAddress),
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = LoginSessionListRateLimit.PermitLimit,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                }));
        });
        return services;
    }

    /// <summary>
    /// 공용 인프라 서비스 등록
    /// </summary>
    public static IServiceCollection AddCommonInfrastructure(this IServiceCollection services)
    {
        services.AddTransient<ILogService, LogService>();
        services.AddHttpContextAccessor();
        return services;
    }

    /// <summary>
    /// 문자 발송기를 등록한다. Sms:Provider가 비었거나 Dev이면 실제로 발송하지 않는 DevSmsSender를, Solapi이면 5초 타임아웃의 typed HttpClient인 SolapiSmsSender를 등록한다.
    /// Dev인데 Development 환경이 아니거나, Solapi인데 Sms:Solapi의 ApiKey·ApiSecret·Sender가 비었거나 Sender가 숫자만이 아니거나, Provider가 그 밖의 값이면 기동할 때 InvalidOperationException을 던진다.
    /// </summary>
    public static IServiceCollection AddSmsInfrastructure(this IServiceCollection services, IConfiguration configuration,
        IHostEnvironment environment)
    {
        var provider = configuration["Sms:Provider"];
        if (string.IsNullOrWhiteSpace(provider) || provider.Trim().Equals("Dev", StringComparison.OrdinalIgnoreCase))
        {
            // 발송하지 않고 성공을 돌려주므로, 운영에서 문자가 나간 것처럼 보이지 않게 Development에서만 허용한다
            if (!environment.IsDevelopment())
                throw new InvalidOperationException("Sms:Provider가 Dev이면 Development 환경에서만 기동합니다. (Solapi로 설정)");
            services.AddSingleton<ISmsSender, DevSmsSender>();
            return services;
        }

        if (!provider.Trim().Equals("Solapi", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Sms:Provider 설정이 올바르지 않습니다. (Dev 또는 Solapi)");

        var solapi = configuration.GetSection("Sms:Solapi");
        RequireText(solapi, "ApiKey");
        RequireText(solapi, "ApiSecret");
        var sender = RequireText(solapi, "Sender");
        if (!sender.All(char.IsAsciiDigit))
            throw new InvalidOperationException($"{solapi.Path}:Sender 설정은 숫자만 써야 합니다.");

        services.Configure<SolapiOptions>(solapi);
        services.AddHttpClient<ISmsSender, SolapiSmsSender>(client =>
        {
            client.BaseAddress = new Uri("https://api.solapi.com/");
            client.Timeout = TimeSpan.FromSeconds(5);
        });
        return services;
    }

    /// <summary>
    /// 인증 설정(Jwt·Crypto·QrLogin)을 검증하고 해시·전화번호 암호화·토큰 서비스를 Singleton으로 등록한다.
    /// 설정이 비었거나 URI·키 형식과 길이가 맞지 않거나 두 키가 같으면 기동할 때 InvalidOperationException을 던진다.
    /// </summary>
    public static IServiceCollection AddAuthInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var jwt = configuration.GetSection("Jwt");
        var crypto = configuration.GetSection("Crypto");
        var qrLogin = configuration.GetSection("QrLogin");

        RequireText(jwt, "Issuer");
        RequireText(jwt, "Audience");
        var signingKey = RequireKey(jwt, "SigningKey", minBytes: 32, exact: false);
        var phoneEncKey = RequireKey(crypto, "PhoneEncKey", minBytes: 32, exact: true);
        var hashKey = RequireKey(crypto, "HashKey", minBytes: 32, exact: false);
        var verificationUri = RequireText(qrLogin, "VerificationUri");
        if (!Uri.TryCreate(verificationUri, UriKind.Absolute, out var parsedVerificationUri)
            || (parsedVerificationUri.Scheme != Uri.UriSchemeHttp && parsedVerificationUri.Scheme != Uri.UriSchemeHttps))
            throw new InvalidOperationException("QrLogin:VerificationUri 설정이 올바르지 않습니다.");

        RequireDistinct(signingKey, "Jwt:SigningKey", phoneEncKey, "Crypto:PhoneEncKey");
        RequireDistinct(signingKey, "Jwt:SigningKey", hashKey, "Crypto:HashKey");
        RequireDistinct(phoneEncKey, "Crypto:PhoneEncKey", hashKey, "Crypto:HashKey");

        services.Configure<JwtOptions>(jwt);
        services.Configure<CryptoOptions>(crypto);
        services.Configure<QrLoginOptions>(qrLogin);
        services.AddSingleton<IHashService, HashService>();
        services.AddSingleton<IPhoneCryptoService, PhoneCryptoService>();
        services.AddSingleton<ITokenService, TokenService>();
        return services;
    }

    /// <summary>
    /// 카카오 설정(Kakao), 공공데이터 설정(PublicData), 출발 안내 설정(DepartureGuide), 대기 장소 설정(WaitingPlace), 픽업 링크 설정(PickupLink)을 등록한다.
    /// Kakao:RestApiKey와 PublicData:ServiceKey가 비어 있어도 기동은 하며, 이때 해당 클라이언트는 외부 API를 부르지 않고 null을 돌려준다.
    /// PickupLink:BaseUrl이 http(s) 절대 URI가 아니거나 PickupLink:MaxAttempts·InFlightTimeoutSec가 1보다 작으면 기동할 때 InvalidOperationException을 던진다.
    /// </summary>
    public static IServiceCollection AddMapInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<KakaoOptions>(configuration.GetSection("Kakao"));
        services.Configure<PublicDataOptions>(configuration.GetSection("PublicData"));
        services.Configure<DepartureGuideOptions>(configuration.GetSection("DepartureGuide"));
        services.Configure<WaitingPlaceOptions>(configuration.GetSection("WaitingPlace"));

        var pickupLink = configuration.GetSection("PickupLink");
        var pickupLinkOptions = pickupLink.Get<PickupLinkOptions>() ?? new PickupLinkOptions();
        if (!Uri.TryCreate(pickupLinkOptions.BaseUrl, UriKind.Absolute, out var parsedBaseUrl)
            || (parsedBaseUrl.Scheme != Uri.UriSchemeHttp && parsedBaseUrl.Scheme != Uri.UriSchemeHttps))
            throw new InvalidOperationException("PickupLink:BaseUrl 설정이 올바르지 않습니다.");
        if (pickupLinkOptions.MaxAttempts < 1)
            throw new InvalidOperationException("PickupLink:MaxAttempts 설정은 1 이상이어야 합니다.");
        if (pickupLinkOptions.InFlightTimeoutSec < 1)
            throw new InvalidOperationException("PickupLink:InFlightTimeoutSec 설정은 1 이상이어야 합니다.");
        services.Configure<PickupLinkOptions>(pickupLink);

        services.AddHttpClient<IWalkRouteClient, KakaoWalkRouteClient>(client =>
        {
            client.BaseAddress = new Uri("https://dapi.kakao.com/");
            client.Timeout = TimeSpan.FromSeconds(3);
        });
        services.AddHttpClient<IParkingRealtimeClient, KoroadParkingRealtimeClient>((serviceProvider, client) =>
        {
            client.BaseAddress = new Uri("https://apis.data.go.kr/B553881/Parking_v1/");
            client.Timeout = TimeSpan.FromMilliseconds(
                serviceProvider.GetRequiredService<IOptions<WaitingPlaceOptions>>().Value.RealtimeTimeoutMs);
        });
        return services;
    }

    // 메시지에는 설정 이름만 넣고 값은 넣지 않는다
    private static string RequireText(IConfigurationSection section, string name)
    {
        var value = section[name];
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"{section.Path}:{name} 설정이 비어 있습니다.");
        return value;
    }

    private static byte[] RequireKey(IConfigurationSection section, string name, int minBytes, bool exact)
    {
        var value = RequireText(section, name);
        var buffer = new byte[value.Length];
        if (!Convert.TryFromBase64String(value, buffer, out var length))
            throw new InvalidOperationException($"{section.Key}:{name} 설정이 base64 형식이 아닙니다.");

        if (exact ? length != minBytes : length < minBytes)
            throw new InvalidOperationException(
                $"{section.Key}:{name} 설정의 길이가 맞지 않습니다. (필요: {minBytes}바이트{(exact ? "" : " 이상")})");

        return buffer[..length];
    }

    // 디코딩한 바이트로 비교한다. 메시지에는 설정 이름만 넣는다
    private static void RequireDistinct(byte[] first, string firstName, byte[] second, string secondName)
    {
        if (first.Length == second.Length && CryptographicOperations.FixedTimeEquals(first, second))
            throw new InvalidOperationException($"{firstName}, {secondName} 두 설정이 같은 값입니다. 키마다 다른 값을 씁니다.");
    }
}
