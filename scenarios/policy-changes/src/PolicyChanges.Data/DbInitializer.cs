using Microsoft.EntityFrameworkCore;
using PolicyChanges.Data.Entities;
using PolicyChanges.Data.Services;

namespace PolicyChanges.Data;

public static class DbInitializer
{
    public const string SamplePartnerKey = "pc_dev_nordbroker_5f2a91c3";

    public static async Task InitializeAsync(PolicyChangesContext db, bool seedSampleData)
    {
        if (!db.IsSqlServer)
        {
            var file = db.Database.GetDbConnection().DataSource;
            if (!string.IsNullOrEmpty(file) && Path.GetDirectoryName(Path.GetFullPath(file)) is { } dir)
                Directory.CreateDirectory(dir);
            await db.Database.EnsureCreatedAsync();
        }

        if (!seedSampleData || await db.Partners.AnyAsync()) return;

        db.Partners.AddRange(
            new Partner { Code = "NORDBROKER", Name = "Nord Insurance Brokers Oy", ApiKeyHash = ApiKeys.Hash(SamplePartnerKey), CreatedUtc = DateTime.UtcNow },
            new Partner { Code = "AUTOHUSET", Name = "Autohuset Agentur AB", ApiKeyHash = ApiKeys.Hash("pc_dev_autohuset_9e04b7d1"), CreatedUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();
    }
}
