namespace Humongous.Quoting.Data;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary><c>SqlServer</c> or <c>Sqlite</c>.</summary>
    public string Provider { get; set; } = "SqlServer";

    /// <summary>Create the schema on start (development only; production uses the scripts in deploy\sql).</summary>
    public bool EnsureCreated { get; set; }

    public bool SeedSampleData { get; set; }
}
