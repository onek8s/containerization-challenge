using Microsoft.Extensions.DependencyInjection;

namespace Lucerne.Lending.CoreBanking.Simulator;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCoreSimulator(this IServiceCollection services, bool withEventSource = false)
    {
        services.AddSingleton<SimulatorDatabase>();
        services.AddSingleton<SimulatorOperations>();
        services.AddSingleton<ICoreCustomerService, SimulatorCustomerService>();
        services.AddSingleton<ICoreLoanService, SimulatorLoanService>();
        services.AddSingleton<ICoreReferenceData, SimulatorReferenceData>();
        if (withEventSource) services.AddSingleton<ICoreEventSource, SimulatorEventSource>();
        return services;
    }
}
