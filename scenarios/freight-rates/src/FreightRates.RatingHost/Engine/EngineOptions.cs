namespace FreightRates.RatingHost.Engine;

public sealed class EngineOptions
{
    public const string Section = "RatingEngine";

    /// <summary>Meridian (RateEngine SDK) or Simulator (fixed test rates for development).</summary>
    public string Provider { get; set; } = "Meridian";

    /// <summary>RateEngine SDK install folder; MRSRATE32.DLL is loaded from its bin folder.</summary>
    public string SdkPath { get; set; } = @"C:\Program Files (x86)\Meridian\RateEngine 7.2";

    public string DataPath { get; set; } = @"D:\MRS\Tariffs";

    public string LicenceFile { get; set; } = @"D:\MRS\Licence\contoso.mrl";

    /// <summary>Licence password protected with protect-password on this server.</summary>
    public string EncryptedPassword { get; set; } = "";

    public string DefaultTariff { get; set; } = "CZ560200";

    public string Country { get; set; } = "USA";

    public bool DeficitRating { get; set; } = true;

    public string IncomingPath { get; set; } = @"D:\MRS\Incoming";

    public string ArchivePath { get; set; } = @"D:\MRS\Archive";

    public int CallTimeoutSeconds { get; set; } = 15;
}
