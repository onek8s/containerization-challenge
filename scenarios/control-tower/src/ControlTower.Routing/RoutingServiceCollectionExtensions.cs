using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Proseware.ControlTower.Routing;

public static class RoutingServiceCollectionExtensions
{
    public static IServiceCollection AddRoutePlanning(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RoutingOptions>(configuration.GetSection(RoutingOptions.Section));
        services.AddSingleton<GreatCircleRoutePlanner>();
        var routerDb = configuration[$"{RoutingOptions.Section}:RouterDbPath"];
        if (!string.IsNullOrWhiteSpace(routerDb))
        {
            services.AddSingleton<ItineroRoutePlanner>();
            services.AddSingleton<IRoutePlanner>(sp => sp.GetRequiredService<ItineroRoutePlanner>());
        }
        else
        {
            services.AddSingleton<IRoutePlanner>(sp => sp.GetRequiredService<GreatCircleRoutePlanner>());
        }
        services.AddSingleton<EtaCalculator>();
        services.AddSingleton(sp => new RunOptimizer(sp.GetRequiredService<IRoutePlanner>()));
        return services;
    }
}
