using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StudyData.Calculation.Development;
using StudyData.Coding;
using StudyData.Coding.Development;
using StudyData.Core.Services;
using StudyData.Data;
using StudyData.Data.Seed;
using StudyData.Data.Services;
using StudyData.Integration.Labs;
using StudyData.Integration.Odm;

namespace StudyData.Tests;

public sealed class FixedClock : IClock
{
    public DateTime UtcNow { get; set; } = new(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc);
}

/// <summary>In-memory SQLite database with the sample study and its sample subjects.</summary>
public sealed class TestDb : IDisposable
{
    private readonly SqliteConnection _connection;

    private TestDb(SqliteConnection connection, ServiceProvider services)
    {
        _connection = connection;
        Services = services;
    }

    public ServiceProvider Services { get; }
    public FixedClock Clock => (FixedClock)Services.GetRequiredService<IClock>();

    public static async Task<TestDb> CreateAsync(bool seed = true)
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var services = new ServiceCollection()
            .AddLogging()
            .AddDbContext<StudyDataContext>(o => o.UseSqlite(connection))
            .AddSingleton<IClock, FixedClock>()
            .AddSingleton<IDerivationService, DevelopmentDerivationService>()
            .AddScoped<AuditWriter>()
            .AddScoped<StudyReadService>()
            .AddScoped<SubjectService>()
            .AddScoped<FormEntryService>()
            .AddScoped<QueryService>()
            .AddScoped<ReviewService>()
            .AddScoped<CodingService>()
            .AddScoped<LabImportService>()
            .AddScoped<OdmExporter>()
            .AddSingleton<ICodingEngine, DevelopmentCodingEngine>()
            .AddSingleton<IOptions<CodingOptions>>(Options.Create(new CodingOptions()))
            .BuildServiceProvider();
        await DbInitializer.EnsureCreatedAsync(services, seed);
        return new TestDb(connection, services);
    }

    public IServiceScope Scope() => Services.CreateScope();

    public void Dispose()
    {
        Services.Dispose();
        _connection.Dispose();
    }
}
