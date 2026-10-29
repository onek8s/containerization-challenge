using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StudyData.Coding.Coderis;
using StudyData.Coding.Development;

namespace StudyData.Coding;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddStudyCoding(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(CodingOptions.Section);
        services.Configure<CodingOptions>(section);
        services.AddScoped<CodingService>();

        if ((section["Provider"] ?? "Coderis").Equals("Development", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<ICodingEngine, DevelopmentCodingEngine>();
        }
        else if (OperatingSystem.IsWindows())
        {
            services.AddSingleton<ICodingEngine, CoderisCodingEngine>();
        }
        else
        {
            throw new PlatformNotSupportedException("The Coderis coding provider needs Windows with Coderis AutoCode 6.");
        }

        return services;
    }
}
