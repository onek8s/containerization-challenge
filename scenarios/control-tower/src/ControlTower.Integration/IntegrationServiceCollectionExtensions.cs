using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Proseware.ControlTower.Domain;
using Proseware.ControlTower.Integration.Carriers;
using Proseware.ControlTower.Integration.Customs;
using Proseware.ControlTower.Integration.Edifact;
using Proseware.ControlTower.Integration.Sftp;

namespace Proseware.ControlTower.Integration;

public static class IntegrationServiceCollectionExtensions
{
    /// <summary>Carrier EDIFACT/SFTP and REST integrations ("Carriers" section).</summary>
    public static IServiceCollection AddCarrierIntegrations(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<CarrierIntegrationOptions>(configuration.GetSection(CarrierIntegrationOptions.Section));
        services.TryAddSingleton<IClock, SystemClock>();
        services.AddSingleton<IPartnerFileChannelFactory, PartnerFileChannelFactory>();
        services.AddSingleton<ControlReferences>();
        services.AddSingleton(sp => new IftminBuilder
        {
            OurPartyId = configuration["Carriers:OurPartyId"] ?? "6430012345000",
            OurName = configuration["Carriers:OurName"] ?? "Proseware Freight Oy"
        });
        services.AddSingleton<ICarrierApiClient, BalticExpressClient>();
        services.AddSingleton<ICarrierApiClient, AerofreightClient>();
        services.AddSingleton<ICarrierApiClient, ParcelNordClient>();
        services.AddSingleton<SimulatedCarrierClient>();
        services.AddSingleton<ICarrierApiClientResolver, CarrierApiClientResolver>();
        return services;
    }

    /// <summary>Customs declaration service ("Customs" section, Mode = Live | Simulator | Disabled).</summary>
    public static IServiceCollection AddCustomsIntegration(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<CustomsOptions>(configuration.GetSection(CustomsOptions.Section));
        services.TryAddSingleton<IClock, SystemClock>();
        services.AddSingleton<WcfCustomsGateway>();
        services.AddSingleton<SimulatedCustomsGateway>();
        services.AddSingleton<ICustomsGateway>(sp =>
        {
            var mode = sp.GetRequiredService<IOptions<CustomsOptions>>().Value.Mode;
            return string.Equals(mode, "Simulator", StringComparison.OrdinalIgnoreCase)
                ? sp.GetRequiredService<SimulatedCustomsGateway>()
                : sp.GetRequiredService<WcfCustomsGateway>();
        });
        return services;
    }
}
