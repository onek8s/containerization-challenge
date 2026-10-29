namespace Humongous.Quoting.Rating;

public sealed class RatingOptions
{
    public const string SectionName = "Rating";

    /// <summary><c>RateCore</c> or <c>Simulator</c>.</summary>
    public string Provider { get; set; } = "RateCore";

    /// <summary>ProgID of the RateCore engine class.</summary>
    public string ProgId { get; set; } = "RateCore.Engine";

    /// <summary>Folder with the product rate table databases (BC2023.mdb, BC2024.mdb, ZONES.mdb).</summary>
    public string TablesPath { get; set; } = @"D:\RateCore\Tables";

    /// <summary>Number of rating threads. Each thread holds its own RateCore engine.</summary>
    public int Workers { get; set; } = 4;

    /// <summary>Recreate an engine after this many calculations.</summary>
    public int RecycleAfterCalls { get; set; } = 500;

    public int CallTimeoutSeconds { get; set; } = 30;
}
