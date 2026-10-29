using CityPermits.Gis.Development;
using CityPermits.Gis.Geomatix;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CityPermits.Gis;

public static class GisServiceCollectionExtensions
{
    public static IServiceCollection AddCityGis(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<GisOptions>(configuration.GetSection(GisOptions.Section));
        services.AddMemoryCache();
        var provider = configuration[$"{GisOptions.Section}:Provider"] ?? "Geomatix";
        if (provider.Equals("Development", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IGisService, DevelopmentGisService>();
        }
        else
        {
            // One engine per process: the SDK host is initialised once and bound to its STA thread.
            services.AddSingleton<GeomatixRuntime>();
            services.AddSingleton<IGisService, GeomatixGisService>();
        }
        return services;
    }
}
