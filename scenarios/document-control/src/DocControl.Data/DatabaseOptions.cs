namespace Litware.DocControl.Data;

public sealed class DatabaseOptions
{
    public const string SectionName = "DocControl:Database";

    /// <summary><c>SqlServer</c> (default) or <c>Sqlite</c> (development and tests).</summary>
    public string Provider { get; set; } = "SqlServer";

    public string ConnectionString { get; set; } = "";

    /// <summary>Creates the SQLite schema and sample data on first use.</summary>
    public bool CreateSampleData { get; set; }

    public int CommandTimeoutSeconds { get; set; } = 60;
}
