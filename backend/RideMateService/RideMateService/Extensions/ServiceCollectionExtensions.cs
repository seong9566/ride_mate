using System.Security.Cryptography;
using RideMateService.Comm.Logger;
using RideMateService.Comm.Security;
using RideMateService.Comm.Sms;
using RideMateService.Repository.v1.Auth;
using RideMateService.Service.v1.Auth;

namespace RideMateService.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 도메인 Repository / Service 일괄 등록
    /// </summary>
    public static IServiceCollection AddDomainModules(this IServiceCollection services)
    {
        services.AddAuthModule();
        // 도메인을 추가하면 여기에 한 줄
        return services;
    }

    private static IServiceCollection AddAuthModule(this IServiceCollection services)
    {
        services.AddTransient<IAuthRepository, AuthRepository>();
        services.AddTransient<IAuthService, AuthService>();
        return services;
    }
}

public static class InfrastructureExtensions
{
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
