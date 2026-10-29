using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StudyData.Core.Services;
using StudyData.Data.Services;

namespace StudyData.Data;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// SQL Server in production; a connection string starting with "Data Source=" and ending in .db uses SQLite
    /// (development and tests).
    /// </summary>
    public static IServiceCollection AddStudyData(this IServiceCollection services, IConfiguration configuration)
    {
        var connection = configuration.GetConnectionString("StudyData")
            ?? throw new InvalidOperationException("Connection string 'StudyData' is missing.");

        services.AddDbContext<StudyDataContext>(o =>
        {
            if (IsSqlite(connection))
            {
                o.UseSqlite(connection);
            }
            else
            {
                o.UseSqlServer(connection, sql => sql.EnableRetryOnFailure(3).CommandTimeout(60));
            }
        });

        services.TryAddSingleton<IClock, SystemClock>();
        services.AddScoped<AuditWriter>();
        services.AddScoped<SubjectService>();
        services.AddScoped<FormEntryService>();
        services.AddScoped<QueryService>();
        services.AddScoped<ReviewService>();
        services.AddScoped<StudyReadService>();
        return services;
    }

    public static bool IsSqlite(string connection) =>
        connection.TrimStart().StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase)
        && connection.TrimEnd().TrimEnd(';').EndsWith(".db", StringComparison.OrdinalIgnoreCase)
        || connection.Contains(":memory:", StringComparison.OrdinalIgnoreCase);

    public static void EnsureDataDirectory(string connection)
    {
        if (!IsSqlite(connection))
        {
            return;
        }

        var path = connection.Split(';')[0]["Data Source=".Length..].Trim();
        var dir = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }
    }
}
