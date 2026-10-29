using Lucerne.Lending.CoreBanking;
using Lucerne.Lending.CoreBanking.IbmI;
using Lucerne.Lending.CoreBanking.Simulator;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Lucerne.Lending.Workflow;

public static class CoreBankingSetup
{
    /// <summary>Registers the core banking adapter selected by Core:Provider (IbmI or Simulator).</summary>
    public static IServiceCollection AddCoreBanking(this IServiceCollection services, IConfiguration configuration, bool withEventSource = false)
    {
        var section = configuration.GetSection(CoreOptions.Section);
        services.Configure<CoreOptions>(section);
        var provider = section["Provider"] ?? "IbmI";
        if (string.Equals(provider, "Simulator", StringComparison.OrdinalIgnoreCase))
            return services.AddCoreSimulator(withEventSource);
#pragma warning disable CA1416 // IBM i Access Client Solutions providers
        return services.AddIbmICore(withEventSource);
#pragma warning restore CA1416
    }
}
