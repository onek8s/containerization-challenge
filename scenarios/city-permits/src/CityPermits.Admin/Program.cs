using CityPermits.Contracts;
using CityPermits.Data;
using CityPermits.Data.Seed;
using CityPermits.Gis;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// permits-admin <verb> [options]
//   create-db                 create the schema (development databases only)
//   seed [--cases N] [--addresses path]   reference data and sample cases
//   script-schema [--out file]            SQL Server create script from the model
//   site-check <parcelId>     run site checks through the configured GIS provider
//   gis-health                show GIS licence and replica status
var verb = args.FirstOrDefault() ?? "help";
string? Opt(string name) { var i = Array.IndexOf(args, name); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }

var builder = Host.CreateApplicationBuilder(args.Skip(1).Where(a => a.StartsWith("--environment") || a.Contains('=')).ToArray());
builder.Configuration.AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"), optional: true)
    .AddJsonFile(Path.Combine(AppContext.BaseDirectory, $"appsettings.{builder.Environment.EnvironmentName}.json"), optional: true)
    .AddEnvironmentVariables("PERMITS_");
builder.Services.AddPermitsData(builder.Configuration);
builder.Services.AddCityGis(builder.Configuration);
using var host = builder.Build();
using var scope = host.Services.CreateScope();
var db = scope.ServiceProvider.GetRequiredService<PermitsDbContext>();
if (!db.IsSqlServer && db.Database.GetDbConnection().DataSource is { Length: > 0 } dataSource)
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(dataSource))!);

switch (verb)
{
    case "create-db":
        if (db.IsSqlServer) { Console.Error.WriteLine("Use deploy/sql scripts for SQL Server."); return 2; }
        Console.WriteLine(db.Database.EnsureCreated() ? "Created." : "Already exists.");
        return 0;

    case "seed":
        db.Database.EnsureCreated();
        var addressFile = Opt("--addresses") ?? builder.Configuration["Seed:AddressFile"] ?? "../../samples/address-register.csv";
        var gis = scope.ServiceProvider.GetRequiredService<IGisService>();
        var addresses = SampleData.ReadAddresses(addressFile);
        await SampleData.SeedAsync(db, addresses, int.Parse(Opt("--cases") ?? "120"), DateTime.UtcNow,
            c => c.ParcelId is null ? null : gis.CheckSite(new SiteCheckRequest(c.ParcelId, null)));
        Console.WriteLine($"Cases: {await db.Cases.CountAsync()}, applicants: {await db.Applicants.CountAsync()}");
        return 0;

    case "script-schema":
        var opts = new DbContextOptionsBuilder<PermitsDbContext>().UseSqlServer("Server=.;Database=CityPermits").Options;
        await using (var sqlDb = new PermitsDbContext(opts))
        {
            var script = sqlDb.Database.GenerateCreateScript();
            var outFile = Opt("--out");
            if (outFile is null) Console.WriteLine(script); else await File.WriteAllTextAsync(outFile, script);
        }
        return 0;

    case "site-check":
        var result = scope.ServiceProvider.GetRequiredService<IGisService>().CheckSite(new SiteCheckRequest(args[1], Opt("--footprint")));
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        return 0;

    case "gis-health":
        Console.WriteLine(scope.ServiceProvider.GetRequiredService<IGisService>().Health());
        return 0;

    default:
        Console.WriteLine("Verbs: create-db, seed [--cases N] [--addresses file], script-schema [--out file], site-check <parcelId> [--footprint WKT], gis-health");
        return verb == "help" ? 0 : 1;
}
