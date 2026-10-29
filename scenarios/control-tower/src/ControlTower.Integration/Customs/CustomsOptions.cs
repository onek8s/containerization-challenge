namespace Proseware.ControlTower.Integration.Customs;

public sealed class CustomsOptions
{
    public const string Section = "Customs";

    /// <summary>Live, Simulator or Disabled.</summary>
    public string Mode { get; set; } = "Live";
    public string Endpoint { get; set; } = "https://ws-test.customs.example/declaration/v2";
    /// <summary>Thumbprint of the client certificate issued to Proseware by customs.</summary>
    public string? CertificateThumbprint { get; set; }
    /// <summary>LocalMachine (default) or CurrentUser.</summary>
    public string CertificateStoreLocation { get; set; } = "LocalMachine";
    public string DeclarantEori { get; set; } = "FI01234567";
    public int TimeoutSeconds { get; set; } = 60;
    public int MaxSubmitAttempts { get; set; } = 5;
    public int StatusPollSeconds { get; set; } = 120;
    /// <summary>Simulator: minutes until a declaration is released; every n-th one goes under control.</summary>
    public int SimulatorReleaseMinutes { get; set; } = 2;
    public int SimulatorControlEvery { get; set; } = 7;
}
