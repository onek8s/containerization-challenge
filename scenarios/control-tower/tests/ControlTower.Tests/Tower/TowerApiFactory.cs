using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Proseware.ControlTower.Tower.Controllers;

namespace Proseware.ControlTower.Tests.Tower;

/// <summary>Tower API in Development mode on its own seeded SQLite database and the in-memory bus.</summary>
public sealed class TowerApiFactory : WebApplicationFactory<DashboardController>
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "ct-tower-tests-" + Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Directory.CreateDirectory(_folder);
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:ControlTower", $"Data Source={Path.Combine(_folder, "tower.db")}");
        builder.UseSetting("Database:Provider", "Sqlite");
        builder.UseSetting("Messaging:Transport", "InMemory");
        builder.UseSetting("Authentication:UseDevelopmentUser", "true");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { Directory.Delete(_folder, recursive: true); } catch (IOException) { }
    }
}
