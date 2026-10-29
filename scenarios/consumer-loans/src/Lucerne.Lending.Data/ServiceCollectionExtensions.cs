using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Lucerne.Lending.Data;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddLendingData(this IServiceCollection services, IConfiguration configuration)
    {
        var options = LendingDbOptions.From(configuration);
        services.AddSingleton(options);
        services.AddSingleton<Db>();
        services.AddSingleton<ApplicationRepository>();
        services.AddSingleton<DecisionRepository>();
        services.AddSingleton<DocumentRepository>();
        services.AddSingleton<OutboxRepository>();
        services.AddSingleton<LoanRepository>();
        services.AddSingleton<CollectionRepository>();
        services.AddSingleton<LedgerRepository>();
        services.AddSingleton<JobRunRepository>();
        services.AddSingleton<ProductRepository>();
        services.AddSingleton<ApiClientRepository>();
        return services;
    }
}
