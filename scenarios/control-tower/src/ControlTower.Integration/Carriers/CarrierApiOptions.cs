namespace Proseware.ControlTower.Integration.Carriers;

/// <summary>Settings of one carrier API ("Carriers:Api:&lt;carrier code&gt;").</summary>
public sealed class CarrierApiOptions
{
    /// <summary>Live, Simulator or Disabled.</summary>
    public string Mode { get; set; } = "Live";
    /// <summary>Overrides the base URL stored on the carrier.</summary>
    public string? BaseUrl { get; set; }
    public string? ApiKey { get; set; }
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public string? TokenUrl { get; set; }
    /// <summary>Our customer / account number at the carrier.</summary>
    public string? AccountNumber { get; set; }
    public int TimeoutSeconds { get; set; } = 30;
    /// <summary>Secret shared with the carrier for webhook signatures.</summary>
    public string? WebhookSecret { get; set; }

    public bool IsLive => string.Equals(Mode, "Live", StringComparison.OrdinalIgnoreCase);
    public bool IsSimulator => string.Equals(Mode, "Simulator", StringComparison.OrdinalIgnoreCase);
    public bool IsDisabled => string.Equals(Mode, "Disabled", StringComparison.OrdinalIgnoreCase);
}

public sealed class CarrierIntegrationOptions
{
    public const string Section = "Carriers";

    public Dictionary<string, CarrierApiOptions> Api { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, Sftp.SftpOptions> Sftp { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Per-partner overrides of the IFTSTA status code mapping.</summary>
    public Dictionary<string, Dictionary<string, string>> StatusCodes { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public CarrierApiOptions ApiFor(string code) => Api.TryGetValue(code, out var o) ? o : new CarrierApiOptions { Mode = "Disabled" };
}
