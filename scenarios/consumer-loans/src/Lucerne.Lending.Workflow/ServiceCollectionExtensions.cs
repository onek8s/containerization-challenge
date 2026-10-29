using Lucerne.Lending.Data;
using Lucerne.Lending.Decisioning;
using Lucerne.Lending.Domain;
using Lucerne.Lending.Integration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Lucerne.Lending.Workflow;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddLendingWorkflow(this IServiceCollection services, IConfiguration configuration, bool coreEvents = false)
    {
        services.Configure<LendingOptions>(configuration.GetSection(LendingOptions.Section));
        services.AddLendingData(configuration);
        services.AddLendingIntegrations(configuration);
        services.AddCoreBanking(configuration, coreEvents);
        services.AddAutoMapper(typeof(MappingProfile));
        services.AddSingleton(new DecisionEngine());
        services.AddSingleton(new AffordabilityCalculator());
        services.AddSingleton(new DelinquencyPolicy());
        services.AddSingleton<ApplicationService>();
        services.AddSingleton<ScoringService>();
        services.AddSingleton<UnderwritingService>();
        services.AddSingleton<BookingService>();
        services.AddSingleton<CollectionsService>();
        return services;
    }
}
