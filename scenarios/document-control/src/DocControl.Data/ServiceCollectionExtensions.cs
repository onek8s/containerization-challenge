using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Litware.DocControl.Data;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDocControlData(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.SectionName));
        services.Configure<Storage.StorageOptions>(configuration.GetSection(Storage.StorageOptions.SectionName));
        services.AddSingleton<DbSession>();
        services.AddSingleton<Storage.VaultStore>();
        services.AddSingleton<DocumentRepository>();
        services.AddSingleton<RevisionRepository>();
        services.AddSingleton<TransmittalRepository>();
        services.AddSingleton<DropFileRepository>();
        return services;
    }
}
