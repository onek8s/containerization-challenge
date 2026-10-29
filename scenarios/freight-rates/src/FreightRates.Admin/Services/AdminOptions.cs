namespace FreightRates.Admin.Services;

public sealed class AdminOptions
{
    public const string Section = "Admin";

    /// <summary>Negotiate (Windows sign-in) or Development (everyone is signed in as a local admin).</summary>
    public string Authentication { get; set; } = "Negotiate";

    public string AdminGroup { get; set; } = @"CORP\Freight Rates Admins";

    /// <summary>Incoming folder of the rating host, shared as MRSData$.</summary>
    public string TariffIncomingShare { get; set; } = @"\\ratehost01.corp.local\MRSData$\Incoming";

    public long MaxTariffUploadBytes { get; set; } = 200L * 1024 * 1024;
}
