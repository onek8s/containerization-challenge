using System.Globalization;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace Lucerne.Lending.CoreBanking.Simulator;

public sealed class SimulatorDatabase
{
    private static readonly object InitLock = new();
    private static readonly HashSet<string> Initialized = new();
    private readonly string _connectionString;

    static SimulatorDatabase() => SqlMapper.AddTypeHandler(new DecimalHandler());

    public SimulatorDatabase(IOptions<CoreOptions> options) : this(options.Value.SimulatorDatabase) { }

    public SimulatorDatabase(string path)
    {
        var full = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        _connectionString = new SqliteConnectionStringBuilder { DataSource = full, Cache = SqliteCacheMode.Shared, DefaultTimeout = 30 }.ToString();
        lock (InitLock)
        {
            if (Initialized.Add(full)) Initialize();
        }
    }

    public SqliteConnection Open()
    {
        var cn = new SqliteConnection(_connectionString);
        cn.Open();
        cn.Execute("PRAGMA journal_mode=WAL; PRAGMA busy_timeout=10000;");
        return cn;
    }

    private void Initialize()
    {
        using var cn = Open();
        using (var stream = typeof(SimulatorDatabase).Assembly.GetManifestResourceStream("Lucerne.Lending.CoreBanking.Simulator.schema.sql")!)
        using (var reader = new StreamReader(stream))
            cn.Execute(reader.ReadToEnd());
        if (cn.ExecuteScalar<long>("SELECT COUNT(*) FROM customer") == 0) SimulatorSeed.Apply(cn);
    }

    public static string D(DateOnly d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    public static DateOnly P(string s) => DateOnly.ParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static long Next(SqliteConnection cn, string name, SqliteTransaction? tx = null)
    {
        cn.Execute("INSERT INTO seq(name, value) VALUES (@name, 0) ON CONFLICT(name) DO NOTHING", new { name }, tx);
        cn.Execute("UPDATE seq SET value = value + 1 WHERE name = @name", new { name }, tx);
        return cn.ExecuteScalar<long>("SELECT value FROM seq WHERE name = @name", new { name }, tx);
    }

    public static DateOnly BusinessDate(SqliteConnection cn, SqliteTransaction? tx = null) =>
        P(cn.ExecuteScalar<string>("SELECT date FROM busdate WHERE id = 1", transaction: tx));
}

/// <summary>SQLite returns INTEGER or REAL per row for the same column, so amounts are converted explicitly.</summary>
internal sealed class DecimalHandler : SqlMapper.TypeHandler<decimal>
{
    public override void SetValue(System.Data.IDbDataParameter parameter, decimal value)
    {
        parameter.DbType = System.Data.DbType.Decimal;
        parameter.Value = value;
    }

    public override decimal Parse(object value) => Convert.ToDecimal(value, CultureInfo.InvariantCulture);
}
