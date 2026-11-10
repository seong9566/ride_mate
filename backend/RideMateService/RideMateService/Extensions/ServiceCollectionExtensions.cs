using RideMateService.Comm.Logger;
using RideMateService.Comm.Security;

namespace RideMateService.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 도메인 Repository / Service 일괄 등록
    /// </summary>
    public static IServiceCollection AddDomainModules(this IServiceCollection services)
    {
        // 도메인을 추가하면 여기에 한 줄
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
        return services;
    }

    /// <summary>
    /// 인증 설정(Jwt·Crypto)을 검증하고 해시·전화번호 암호화·토큰 서비스를 Singleton으로 등록한다.
    /// 설정이 비었거나 키 형식·길이가 맞지 않으면 기동할 때 InvalidOperationException을 던진다.
    /// </summary>
    public static IServiceCollection AddAuthInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var jwt = configuration.GetSection("Jwt");
        var crypto = configuration.GetSection("Crypto");

        RequireText(jwt, "Issuer");
        RequireText(jwt, "Audience");
        RequireKey(jwt, "SigningKey", minBytes: 32, exact: false);
        RequireKey(crypto, "PhoneEncKey", minBytes: 32, exact: true);
        RequireKey(crypto, "HashKey", minBytes: 32, exact: false);

        services.Configure<JwtOptions>(jwt);
        services.Configure<CryptoOptions>(crypto);
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

    private static void RequireKey(IConfigurationSection section, string name, int minBytes, bool exact)
    {
        var value = RequireText(section, name);
        var buffer = new byte[value.Length];
        if (!Convert.TryFromBase64String(value, buffer, out var length))
            throw new InvalidOperationException($"{section.Key}:{name} 설정이 base64 형식이 아닙니다.");

        if (exact ? length != minBytes : length < minBytes)
            throw new InvalidOperationException(
                $"{section.Key}:{name} 설정의 길이가 맞지 않습니다. (필요: {minBytes}바이트{(exact ? "" : " 이상")})");
    }
}
