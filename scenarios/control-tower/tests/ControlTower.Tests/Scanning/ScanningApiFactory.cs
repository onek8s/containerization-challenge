using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Proseware.ControlTower.Scanning.Scans;

namespace Proseware.ControlTower.Tests.Scanning;

/// <summary>Scanning API in Development mode on its own seeded SQLite database, device-key sign-in and a file label printer.</summary>
public sealed class ScanningApiFactory : WebApplicationFactory<ScanService>
{
    public const string DeviceHeader = "TC51-TEST-01:test-key";

    public string Folder { get; } = Path.Combine(Path.GetTempPath(), "ct-scan-tests-" + Guid.NewGuid().ToString("N"));

    public string LabelFolder => Path.Combine(Folder, "labels");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Directory.CreateDirectory(Folder);
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:ControlTower", $"Data Source={Path.Combine(Folder, "scan.db")}");
        builder.UseSetting("Database:Provider", "Sqlite");
        builder.UseSetting("Messaging:Transport", "InMemory");
        builder.UseSetting("DataProtection:KeyPath", Path.Combine(Folder, "keys"));
        builder.UseSetting("DeviceKeys:TC51-TEST-01", "test-key");
        builder.UseSetting("Labels:Printers:zt-test:Kind", "File");
        builder.UseSetting("Labels:Printers:zt-test:Address", LabelFolder);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { Directory.Delete(Folder, recursive: true); } catch (IOException) { }
    }
}
