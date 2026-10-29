using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CityPermits.Data;

public static class DataServiceCollectionExtensions
{
    /// <summary>
    /// Database:Provider = SqlServer (default) or Sqlite (development only).
    /// ConnectionStrings:Permits holds the connection string.
    /// </summary>
    public static IServiceCollection AddPermitsData(this IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration["Database:Provider"] ?? "SqlServer";
        var cs = configuration.GetConnectionString("Permits") ?? throw new InvalidOperationException("ConnectionStrings:Permits is not configured.");
        services.AddDbContext<PermitsDbContext>(o =>
        {
            if (provider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
                o.UseSqlite(cs);
            else
                o.UseSqlServer(cs, s =>
                {
                    s.EnableRetryOnFailure(3);
                    s.CommandTimeout(60);
                    s.MigrationsHistoryTable("__EFMigrationsHistory", "permits");
                });
        });
        services.AddSingleton(new ConnectionFactory(provider, cs));
        services.AddScoped<ReferenceAllocator>();
        services.AddScoped<Reporting.ReportQueries>();
        services.AddScoped<OutboxWriter>();
        return services;
    }
}

public sealed class ConnectionFactory
{
    public ConnectionFactory(string provider, string connectionString)
    {
        Provider = provider;
        ConnectionString = connectionString;
    }

    public string Provider { get; }
    public string ConnectionString { get; }
    public bool IsSqlite => Provider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase);

    public System.Data.Common.DbConnection Open()
    {
        System.Data.Common.DbConnection c = IsSqlite
            ? new Microsoft.Data.Sqlite.SqliteConnection(ConnectionString)
            : new Microsoft.Data.SqlClient.SqlConnection(ConnectionString);
        c.Open();
        return c;
    }
}
