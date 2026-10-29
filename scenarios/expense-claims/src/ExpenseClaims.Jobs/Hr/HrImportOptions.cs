namespace ExpenseClaims.Jobs.Hr;

public sealed class HrImportOptions
{
    public const string SectionName = "HrImport";

    /// <summary>Folder where the HR system drops the nightly employee export.</summary>
    public string InboundFolder { get; set; } = @"D:\Interfaces\HR\Inbound";

    public string FilePattern { get; set; } = "employees_*.csv";

    /// <summary>Safety limit: the import stops when more than this share of active employees would be deactivated.</summary>
    public double MaxDeactivationShare { get; set; } = 0.1;
}
