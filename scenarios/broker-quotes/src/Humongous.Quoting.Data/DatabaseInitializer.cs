using Humongous.Quoting.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Humongous.Quoting.Data;

public sealed class DatabaseInitializer(QuotingDbContext db, DatabaseOptions options, ILogger<DatabaseInitializer> logger)
{
    public async Task InitialiseAsync(CancellationToken ct = default)
    {
        if (options.EnsureCreated)
        {
            if (await db.Database.EnsureCreatedAsync(ct))
                logger.LogInformation("Created quoting database schema");
        }

        if (options.SeedSampleData && !await db.Agencies.AnyAsync(ct))
        {
            db.Agencies.AddRange(SampleData.Agencies());
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Seeded sample agencies");
        }
    }
}

public static class SampleData
{
    public static IEnumerable<Agency> Agencies() =>
    [
        new() { Code = "ALP01", Name = "Alpine Insurance Brokers Ltd", FcaNumber = "305112", CommissionRate = 0.20m, DocumentEmail = "documents@alpine-brokers.example", CreatedUtc = DateTime.UtcNow },
        new() { Code = "NWB02", Name = "Northbridge Commercial Insurance", FcaNumber = "412877", CommissionRate = 0.175m, DocumentEmail = "newbusiness@northbridge.example", CreatedUtc = DateTime.UtcNow },
        new() { Code = "HRP03", Name = "Harrogate Risk Partners LLP", FcaNumber = "598001", CommissionRate = 0.225m, DocumentEmail = "admin@harrogaterisk.example", CreatedUtc = DateTime.UtcNow },
    ];
}
