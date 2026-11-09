using RideMateService.Comm.Logger;
using RideMateService.Repository.v1.{도메인};
using RideMateService.Service.v1.{도메인};

namespace RideMateService.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 도메인 Repository / Service 일괄 등록
    /// </summary>
    public static IServiceCollection AddDomainModules(this IServiceCollection services)
    {
        services.Add{도메인}Module();
        // 도메인을 추가하면 여기에 한 줄
        return services;
    }

    private static IServiceCollection Add{도메인}Module(this IServiceCollection services)
    {
        services.AddTransient<I{도메인}Repository, {도메인}Repository>();
        services.AddTransient<I{도메인}Service, {도메인}Service>();
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
}
