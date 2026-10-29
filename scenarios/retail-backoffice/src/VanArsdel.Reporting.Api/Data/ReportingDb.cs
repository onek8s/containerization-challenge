using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Reflection;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace VanArsdel.Reporting.Api.Data;

/// <summary>
/// Plain ADO.NET + Dapper. The reporting database is fed by events and queried with hand-written SQL,
/// which is what the BI team asked for (they read the same tables from Power BI).
/// </summary>
public class ReportingDb
{
    private readonly string _connectionString;

    public ReportingDb(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("Reporting");
        IsSqlite = string.Equals(configuration["Database:Provider"], "Sqlite", StringComparison.OrdinalIgnoreCase);
    }

    public bool IsSqlite { get; }

    public async Task<DbConnection> OpenAsync(CancellationToken cancellationToken = default)
    {
        DbConnection connection = IsSqlite ? new SqliteConnection(_connectionString) : new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    /// <summary>"TOP n" vs "LIMIT n".</summary>
    public string Top(string selectList, string rest, int n) =>
        IsSqlite ? $"SELECT {selectList} {rest} LIMIT {n}" : $"SELECT TOP ({n}) {selectList} {rest}";

    public string Upsert(string table, string[] keyColumns, string[] valueColumns)
    {
        var all = keyColumns.Concat(valueColumns).ToArray();
        if (IsSqlite)
        {
            return $"INSERT INTO {table} ({string.Join(", ", all)}) VALUES ({string.Join(", ", all.Select(c => "@" + c))}) " +
                   $"ON CONFLICT ({string.Join(", ", keyColumns)}) DO UPDATE SET {string.Join(", ", valueColumns.Select(c => c + " = excluded." + c))};";
        }
        return $"MERGE dbo.{table} WITH (HOLDLOCK) AS t USING (SELECT {string.Join(", ", all.Select(c => "@" + c + " AS " + c))}) AS s " +
               $"ON {string.Join(" AND ", keyColumns.Select(c => "t." + c + " = s." + c))} " +
               $"WHEN MATCHED THEN UPDATE SET {string.Join(", ", valueColumns.Select(c => "t." + c + " = s." + c))} " +
               $"WHEN NOT MATCHED THEN INSERT ({string.Join(", ", all)}) VALUES ({string.Join(", ", all.Select(c => "s." + c))});";
    }

    /// <summary>Records the message id in the same transaction as the read model change. False when seen before.</summary>
    public async Task<bool> TryMarkProcessedAsync(DbConnection connection, DbTransaction transaction, Guid messageId, string consumer)
    {
        var seen = await connection.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM ProcessedMessage WHERE MessageId = @messageId",
            new { messageId = Key(messageId) }, transaction);
        if (seen > 0) return false;
        await connection.ExecuteAsync("INSERT INTO ProcessedMessage (MessageId, Consumer, ProcessedUtc) VALUES (@messageId, @consumer, @now)",
            new { messageId = Key(messageId), consumer, now = DateTime.UtcNow }, transaction);
        return true;
    }

    /// <summary>GUIDs are stored as text in SQLite.</summary>
    public object Key(Guid id) => IsSqlite ? id.ToString() : id;

    public void CreateSchema()
    {
        var resource = IsSqlite ? "schema.sqlite.sql" : "schema.sqlserver.sql";
        if (IsSqlite)
        {
            var path = new SqliteConnectionStringBuilder(_connectionString).DataSource;
            var dir = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        }
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("VanArsdel.Reporting.Api.Sql." + resource);
        using var reader = new StreamReader(stream);
        using var connection = OpenAsync().GetAwaiter().GetResult();
        connection.Execute(reader.ReadToEnd());
    }

    public static string SqlServerScript()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("VanArsdel.Reporting.Api.Sql.schema.sqlserver.sql");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public static void RegisterTypeHandlers()
    {
        SqlMapper.AddTypeHandler(new DateTimeHandler());
        SqlMapper.RemoveTypeMap(typeof(DateTime));
        SqlMapper.RemoveTypeMap(typeof(DateTime?));
    }

    private class DateTimeHandler : SqlMapper.TypeHandler<DateTime>
    {
        public override void SetValue(IDbDataParameter parameter, DateTime value)
        {
            parameter.Value = value;
            if (parameter is SqliteParameter) parameter.Value = value.ToString("yyyy-MM-dd HH:mm:ss.fffffff", CultureInfo.InvariantCulture);
        }

        public override DateTime Parse(object value) => value is DateTime d ? d : DateTime.Parse(value.ToString(), CultureInfo.InvariantCulture);
    }
}

public class ReportingDbHealthCheck : IHealthCheck
{
    private readonly ReportingDb _db;

    public ReportingDbHealthCheck(ReportingDb db)
    {
        _db = db;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = await _db.OpenAsync(cancellationToken);
            await connection.ExecuteScalarAsync<int>("SELECT 1");
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Reporting database", ex);
        }
    }
}

public class CurrencyOptions
{
    /// <summary>Budget rate used by finance for the year. Updated by hand in January.</summary>
    public decimal SekPerEur { get; set; } = 11.2m;

    public decimal ToEur(decimal amount, string currency) =>
        string.Equals(currency, "SEK", StringComparison.OrdinalIgnoreCase) ? Math.Round(amount / SekPerEur, 2) : amount;
}
