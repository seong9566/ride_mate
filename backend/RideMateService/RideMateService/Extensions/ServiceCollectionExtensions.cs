using System.Security.Cryptography;
using System.Threading.RateLimiting;
using RideMateService.Comm;
using RideMateService.Comm.Logger;
using RideMateService.Comm.Map;
using RideMateService.Comm.Security;
using RideMateService.Comm.Sms;
using RideMateService.Repository.v1.Auth;
using RideMateService.Repository.v1.Pickup;
using RideMateService.Service.v1.Auth;
using RideMateService.Service.v1.Pickup;

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
        // 문자 사업자가 정해지면 구현체만 바꾼다
        services.AddSingleton<ISmsSender, DevSmsSender>();
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
    /// 카카오 설정(Kakao)과 출발 안내 설정(DepartureGuide)을 등록하고, 도보 경로 클라이언트를 타임아웃 3초의 typed HttpClient로 등록한다.
    /// Kakao:RestApiKey가 비어 있어도 기동은 하며, 이때 클라이언트는 카카오를 부르지 않고 null을 돌려준다.
    /// </summary>
    public static IServiceCollection AddMapInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<KakaoOptions>(configuration.GetSection("Kakao"));
        services.Configure<DepartureGuideOptions>(configuration.GetSection("DepartureGuide"));
        services.AddHttpClient<IWalkRouteClient, KakaoWalkRouteClient>(client =>
        {
            client.BaseAddress = new Uri("https://dapi.kakao.com/");
            client.Timeout = TimeSpan.FromSeconds(3);
        });
        return services;
    }

    // 메시지에는 설정 이름만 넣고 값은 넣지 않는다
    private static string RequireText(IConfigurationSection section, string name)
    {
        var value = section[name];
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"{section.Key}:{name} 설정이 비어 있습니다.");
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
