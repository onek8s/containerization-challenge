using DbUp;
using DbUp.Engine;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Lucerne.Lending.Data;

public static class SchemaUpgrader
{
    /// <summary>Applies the embedded scripts of the configured provider (DbUp journal lending.SchemaVersions).</summary>
    public static void Upgrade(LendingDbOptions options, ILogger? log = null)
    {
        var sqlite = string.Equals(options.Provider, "Sqlite", StringComparison.OrdinalIgnoreCase);
        var folder = sqlite ? ".Scripts.Sqlite." : ".Scripts.SqlServer.";
        var builder = sqlite
            ? DeployChanges.To.SQLiteDatabase(EnsureSqliteFolder(options.ConnectionString))
            : DeployChanges.To.SqlDatabase(options.ConnectionString).JournalToSqlTable("lending", "SchemaVersions");
        var upgrader = builder
            .WithScriptsEmbeddedInAssembly(typeof(SchemaUpgrader).Assembly, name => name.Contains(folder, StringComparison.Ordinal))
            .WithTransactionPerScript()
            .LogToNowhere()
            .Build();
        if (!upgrader.IsUpgradeRequired()) return;
        DatabaseUpgradeResult result = upgrader.PerformUpgrade();
        if (!result.Successful)
            throw new InvalidOperationException($"Database upgrade failed in {result.ErrorScript?.Name}: {result.Error.Message}", result.Error);
        log?.LogInformation("Database upgraded: {Scripts}", string.Join(", ", result.Scripts.Select(s => s.Name)));
    }

    private static string EnsureSqliteFolder(string connectionString)
    {
        var builder = new SqliteConnectionStringBuilder(connectionString);
        var dir = Path.GetDirectoryName(Path.GetFullPath(builder.DataSource));
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        return connectionString;
    }
}
