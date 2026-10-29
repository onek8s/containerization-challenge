using Humongous.Quoting.Domain;
using Humongous.Quoting.Rating.RateCore;
using Humongous.Quoting.Rating.Simulator;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Humongous.Quoting.Rating;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRating(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(RatingOptions.SectionName);
        services.Configure<RatingOptions>(section);
        var provider = section.GetValue<string>("Provider") ?? "RateCore";

        if (string.Equals(provider, "Simulator", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<SimulatorRatingEngine>();
            services.AddSingleton<IRatingEngine>(sp => sp.GetRequiredService<SimulatorRatingEngine>());
            services.AddSingleton<IRatingEngineStatus>(sp => sp.GetRequiredService<SimulatorRatingEngine>());
        }
        else if (OperatingSystem.IsWindows())
        {
            services.AddSingleton<RateCoreEnginePool>();
            services.AddSingleton<IRatingEngine>(sp => sp.GetRequiredService<RateCoreEnginePool>());
            services.AddSingleton<IRatingEngineStatus>(sp => sp.GetRequiredService<RateCoreEnginePool>());
        }
        else
        {
            throw new PlatformNotSupportedException($"Rating provider '{provider}' needs Windows.");
        }

        return services;
    }
}
