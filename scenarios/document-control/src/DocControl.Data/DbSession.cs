using System.Data.Common;
using System.Reflection;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace Litware.DocControl.Data;

/// <summary>Creates connections and knows the table prefix and dialect for the configured provider.</summary>
public sealed class DbSession
{
    private static int _sqliteInitialised;
    private readonly DatabaseOptions _options;

    public DbSession(IOptions<DatabaseOptions> options)
    {
        _options = options.Value;
        IsSqlite = string.Equals(_options.Provider, "Sqlite", StringComparison.OrdinalIgnoreCase);
        Dialect = IsSqlite ? SqlDialect.Sqlite : SqlDialect.SqlServer;
        Schema = IsSqlite ? "" : "dc.";
        if (string.IsNullOrWhiteSpace(_options.ConnectionString))
            throw new InvalidOperationException($"{DatabaseOptions.SectionName}:ConnectionString is not configured.");
    }

    /// <summary>Lets tests start from an empty database file.</summary>
    internal static void ResetInitialisation() => Interlocked.Exchange(ref _sqliteInitialised, 0);

    public bool IsSqlite { get; }
    public SqlDialect Dialect { get; }

    /// <summary>"dc." on SQL Server, empty on SQLite.</summary>
    public string Schema { get; }

    public int CommandTimeout => _options.CommandTimeoutSeconds;

    /// <summary>Replaces <c>{s}</c> in the statement with the schema prefix.</summary>
    public string Sql(string statement) => statement.Replace("{s}", Schema, StringComparison.Ordinal);

    public async Task<DbConnection> OpenAsync(CancellationToken cancellationToken = default)
    {
        DbConnection connection;
        if (IsSqlite)
        {
            var builder = new SqliteConnectionStringBuilder(_options.ConnectionString);
            var folder = Path.GetDirectoryName(Path.GetFullPath(builder.DataSource));
            if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
            connection = new SqliteConnection(builder.ConnectionString);
        }
        else
        {
            connection = new SqlConnection(_options.ConnectionString);
        }
        await connection.OpenAsync(cancellationToken);
        if (IsSqlite)
        {
            await connection.ExecuteAsync("PRAGMA foreign_keys = ON; PRAGMA busy_timeout = 5000;");
            if (Interlocked.Exchange(ref _sqliteInitialised, 1) == 0)
                await InitialiseSqliteAsync(connection);
        }
        return connection;
    }

    private async Task InitialiseSqliteAsync(DbConnection connection)
    {
        var exists = await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'Documents'");
        if (exists > 0) return;

        await connection.ExecuteAsync("PRAGMA journal_mode = WAL;");
        await connection.ExecuteAsync(ReadResource("sqlite-schema.sql"));
        if (_options.CreateSampleData)
            await connection.ExecuteAsync(ReadResource("sqlite-sample-data.sql"));
    }

    private static string ReadResource(string name)
    {
        var asm = Assembly.GetExecutingAssembly();
        var resource = asm.GetManifestResourceNames().Single(n => n.EndsWith(name, StringComparison.OrdinalIgnoreCase));
        using var stream = asm.GetManifestResourceStream(resource)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
