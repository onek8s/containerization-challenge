namespace StudyData.Calculation;

public class CalculationOptions
{
    public const string Section = "Calculation";

    /// <summary>Excel (production) or Development (built-in formulas of the sample study, no Excel needed).</summary>
    public string Provider { get; set; } = "Excel";

    /// <summary>Study folders with the validated workbooks: {root}\{study}\calculations\{file}.</summary>
    public string WorkbookRoot { get; set; } = @"\\corp.local\dfs\StudyData\Studies";

    /// <summary>Local copy of verified workbooks, opened read-only by Excel.</summary>
    public string LocalCache { get; set; } = @"C:\StudyData\Workbooks";

    /// <summary>Number of Excel instances. Each one serves one calculation at a time.</summary>
    public int PoolSize { get; set; } = 2;

    /// <summary>Recycle an Excel instance after this many calculations.</summary>
    public int MaxCalculationsPerInstance { get; set; } = 500;

    public TimeSpan CalculationTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Wait for a free instance before the save is refused.</summary>
    public TimeSpan QueueTimeout { get; set; } = TimeSpan.FromSeconds(20);
}
