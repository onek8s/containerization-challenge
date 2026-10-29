using System.Data;
using System.Data.Common;
using System.Text.RegularExpressions;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

namespace Lucerne.Lending.Data;

public sealed class LendingDbOptions
{
    /// <summary>SqlServer (default) or Sqlite (development).</summary>
    public string Provider { get; set; } = "SqlServer";
    public string ConnectionString { get; set; } = "";

    public static LendingDbOptions From(IConfiguration configuration) => new()
    {
        Provider = configuration["Database:Provider"] ?? "SqlServer",
        ConnectionString = configuration.GetConnectionString("Lending")
            ?? throw new InvalidOperationException("Connection string 'Lending' is missing.")
    };
}

/// <summary>
/// Connection factory. Statements are written for SQL Server; for the SQLite development database a few
/// constructs are rewritten (schema prefix, TOP, table hints, SCOPE_IDENTITY, OFFSET/FETCH).
/// </summary>
public sealed class Db
{
    private static readonly Regex Top = new(@"SELECT TOP \((@?\w+)\)", RegexOptions.Compiled);
    private static readonly Regex Hints = new(@"WITH \((UPDLOCK|READPAST|ROWLOCK|HOLDLOCK|NOLOCK)(, ?(UPDLOCK|READPAST|ROWLOCK|HOLDLOCK|NOLOCK))*\)", RegexOptions.Compiled);
    private static readonly Regex OffsetFetch = new(@"OFFSET (@?\w+) ROWS FETCH NEXT (@?\w+) ROWS ONLY", RegexOptions.Compiled);

    static Db()
    {
        SqlMapper.AddTypeHandler(new DateOnlyHandler());
        SqlMapper.AddTypeHandler(new NullableDateOnlyHandler());
        // SQLite returns INTEGER or REAL per row for the same column; convert explicitly
        SqlMapper.AddTypeHandler(new DecimalHandler());
    }

    public Db(LendingDbOptions options)
    {
        Options = options;
        IsSqlite = string.Equals(options.Provider, "Sqlite", StringComparison.OrdinalIgnoreCase);
    }

    public LendingDbOptions Options { get; }
    public bool IsSqlite { get; }

    public async Task<DbConnection> OpenAsync(CancellationToken ct = default)
    {
        DbConnection cn = IsSqlite ? new SqliteConnection(Options.ConnectionString) : new SqlConnection(Options.ConnectionString);
        await cn.OpenAsync(ct);
        if (IsSqlite) await cn.ExecuteAsync("PRAGMA journal_mode=WAL; PRAGMA busy_timeout=10000; PRAGMA foreign_keys=ON;");
        return cn;
    }

    public string Sql(string sql)
    {
        if (!IsSqlite) return sql;
        var s = sql.Replace("lending.", "", StringComparison.Ordinal)
            .Replace("CAST(SCOPE_IDENTITY() AS bigint)", "last_insert_rowid()", StringComparison.Ordinal)
            .Replace("ISNULL(", "IFNULL(", StringComparison.Ordinal);
        s = Hints.Replace(s, "");
        s = OffsetFetch.Replace(s, "LIMIT $2 OFFSET $1");
        var top = Top.Match(s);
        if (top.Success) s = Top.Replace(s, "SELECT", 1).TrimEnd().TrimEnd(';') + " LIMIT " + top.Groups[1].Value;
        return s;
    }

    public async Task<IReadOnlyList<T>> QueryAsync<T>(string sql, object? param = null, IDbTransaction? tx = null, CancellationToken ct = default)
    {
        if (tx != null) return (await tx.Connection!.QueryAsync<T>(Sql(sql), param, tx)).AsList();
        await using var cn = await OpenAsync(ct);
        return (await cn.QueryAsync<T>(new CommandDefinition(Sql(sql), param, cancellationToken: ct))).AsList();
    }

    public async Task<T?> FirstOrDefaultAsync<T>(string sql, object? param = null, IDbTransaction? tx = null, CancellationToken ct = default)
    {
        if (tx != null) return await tx.Connection!.QueryFirstOrDefaultAsync<T>(Sql(sql), param, tx);
        await using var cn = await OpenAsync(ct);
        return await cn.QueryFirstOrDefaultAsync<T>(new CommandDefinition(Sql(sql), param, cancellationToken: ct));
    }

    public async Task<int> ExecuteAsync(string sql, object? param = null, IDbTransaction? tx = null, CancellationToken ct = default)
    {
        if (tx != null) return await tx.Connection!.ExecuteAsync(Sql(sql), param, tx);
        await using var cn = await OpenAsync(ct);
        return await cn.ExecuteAsync(new CommandDefinition(Sql(sql), param, cancellationToken: ct));
    }

    public async Task<T?> ScalarAsync<T>(string sql, object? param = null, IDbTransaction? tx = null, CancellationToken ct = default)
    {
        if (tx != null) return await tx.Connection!.ExecuteScalarAsync<T>(Sql(sql), param, tx);
        await using var cn = await OpenAsync(ct);
        return await cn.ExecuteScalarAsync<T>(new CommandDefinition(Sql(sql), param, cancellationToken: ct));
    }

    public async Task<T> InTransactionAsync<T>(Func<DbTransaction, Task<T>> work, CancellationToken ct = default)
    {
        await using var cn = await OpenAsync(ct);
        await using var tx = await cn.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        var result = await work(tx);
        await tx.CommitAsync(ct);
        return result;
    }

    public async Task<long> NextSequenceAsync(string name, IDbTransaction? tx = null)
    {
        if (IsSqlite)
        {
            await ExecuteAsync("INSERT INTO Sequence(Name, Value) VALUES (@name, 0) ON CONFLICT(Name) DO NOTHING", new { name }, tx);
            await ExecuteAsync("UPDATE Sequence SET Value = Value + 1 WHERE Name = @name", new { name }, tx);
            return await ScalarAsync<long>("SELECT Value FROM Sequence WHERE Name = @name", new { name }, tx);
        }
        var value = await ScalarAsync<long?>(
            "UPDATE lending.Sequence WITH (UPDLOCK, HOLDLOCK) SET Value = Value + 1 OUTPUT inserted.Value WHERE Name = @name", new { name }, tx);
        return value ?? await ScalarAsync<long>(
            "INSERT INTO lending.Sequence(Name, Value) OUTPUT inserted.Value VALUES (@name, 1)", new { name }, tx);
    }
}

internal sealed class DecimalHandler : SqlMapper.TypeHandler<decimal>
{
    public override void SetValue(IDbDataParameter parameter, decimal value)
    {
        parameter.DbType = DbType.Decimal;
        parameter.Value = value;
    }

    public override decimal Parse(object value) => Convert.ToDecimal(value, System.Globalization.CultureInfo.InvariantCulture);
}

internal sealed class DateOnlyHandler : SqlMapper.TypeHandler<DateOnly>
{
    public override void SetValue(IDbDataParameter parameter, DateOnly value)
    {
        parameter.DbType = DbType.Date;
        parameter.Value = value.ToDateTime(TimeOnly.MinValue);
    }

    public override DateOnly Parse(object value) => value switch
    {
        DateTime dt => DateOnly.FromDateTime(dt),
        string s => DateOnly.FromDateTime(DateTime.Parse(s)),
        _ => DateOnly.FromDateTime(Convert.ToDateTime(value))
    };
}

internal sealed class NullableDateOnlyHandler : SqlMapper.TypeHandler<DateOnly?>
{
    public override void SetValue(IDbDataParameter parameter, DateOnly? value)
    {
        parameter.DbType = DbType.Date;
        parameter.Value = value.HasValue ? value.Value.ToDateTime(TimeOnly.MinValue) : DBNull.Value;
    }

    public override DateOnly? Parse(object value) => value is null or DBNull ? null : new DateOnlyHandler().Parse(value);
}
