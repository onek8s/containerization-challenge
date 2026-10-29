using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Proseware.ControlTower.Data;
using Proseware.ControlTower.Data.Seed;

namespace Proseware.ControlTower.LabTools.Commands;

internal static class LabDatabase
{
    public static ControlTowerDbContext Open(string connectionString, string provider)
    {
        var options = new DbContextOptionsBuilder<ControlTowerDbContext>();
        if (string.Equals(provider, "Sqlite", StringComparison.OrdinalIgnoreCase))
            options.UseSqlite(connectionString);
        else
            options.UseSqlServer(connectionString, sql =>
            {
                sql.UseNetTopologySuite();
                sql.CommandTimeout(300);
            });
        return new ControlTowerDbContext(options.Options);
    }

    public static string GuessProvider(string connectionString) =>
        connectionString.Contains("Data Source=", StringComparison.OrdinalIgnoreCase)
        && !connectionString.Contains("Initial Catalog", StringComparison.OrdinalIgnoreCase)
        && (connectionString.Contains(".db", StringComparison.OrdinalIgnoreCase) || connectionString.Contains(":memory:", StringComparison.OrdinalIgnoreCase))
            ? "Sqlite"
            : "SqlServer";
}

/// <summary>Loads master data and sample shipments into an empty Control Tower database.</summary>
public sealed class SeedCommand : ILabCommand
{
    public string Name => "seed";

    public string Usage => "--connection <connection string> [--provider SqlServer|Sqlite] [--create] [--shipments 180]";

    public async Task<int> RunAsync(string[] args, CancellationToken cancellationToken)
    {
        var a = new LabArgs(args);
        var connection = a.Required("connection");
        var provider = a.Get("provider") ?? LabDatabase.GuessProvider(connection);
        await using var db = LabDatabase.Open(connection, provider);

        if (a.Has("create"))
        {
            var created = await db.Database.EnsureCreatedAsync(cancellationToken);
            Console.WriteLine(created ? "Database schema created." : "Database already exists, schema left as is.");
        }

        if (await db.Customers.AnyAsync(cancellationToken))
        {
            Console.WriteLine("The database already contains master data; nothing was loaded.");
            return 0;
        }

        var seed = new SeedData(db, DateTimeOffset.UtcNow) { ShipmentCount = a.GetInt("shipments", 180) };
        await seed.RunAsync(cancellationToken);
        Console.WriteLine($"Loaded {await db.Customers.CountAsync(cancellationToken)} customers, {await db.Carriers.CountAsync(cancellationToken)} carriers, " +
                          $"{await db.Locations.CountAsync(cancellationToken)} locations, {await db.Vehicles.CountAsync(cancellationToken)} vehicles, " +
                          $"{await db.Shipments.CountAsync(cancellationToken)} shipments.");
        return 0;
    }
}

/// <summary>Prints the SQL Server DDL of the EF model (used to maintain db\01-Schema.sql).</summary>
public sealed class SchemaCommand : ILabCommand
{
    public string Name => "schema";

    public string Usage => "[--out <file>]";

    public Task<int> RunAsync(string[] args, CancellationToken cancellationToken)
    {
        var a = new LabArgs(args);
        using var db = LabDatabase.Open("Server=.;Database=ControlTower;Integrated Security=false", "SqlServer");
        var script = db.Database.GenerateCreateScript();
        var output = a.Get("out");
        if (output is null) Console.WriteLine(script);
        else
        {
            File.WriteAllText(output, script);
            Console.WriteLine($"Schema written to {output}");
        }
        return Task.FromResult(0);
    }
}
