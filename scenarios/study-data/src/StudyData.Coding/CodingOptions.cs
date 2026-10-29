namespace StudyData.Coding;

public class CodingOptions
{
    public const string Section = "Coding";

    /// <summary>Coderis (production) or Development (small built-in dictionary).</summary>
    public string Provider { get; set; } = "Coderis";

    /// <summary>Licence file issued by Coderis for this server.</summary>
    public string LicenceFile { get; set; } = @"C:\Program Files (x86)\Coderis\AutoCode 6\licence\studydata.lic";

    /// <summary>Dictionary versions loaded by the autocoder.</summary>
    public string MedDraVersion { get; set; } = "27.1";

    public string WhoDrugVersion { get; set; } = "B3 2026-MAR";

    public int MaxCandidates { get; set; } = 5;

    public int BatchSize { get; set; } = 200;
}
