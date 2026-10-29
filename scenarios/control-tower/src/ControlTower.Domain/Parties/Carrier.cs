namespace Proseware.ControlTower.Domain.Parties;

public class Carrier
{
    public int Id { get; set; }
    /// <summary>Short code; SCAC for ocean carriers, IATA prefix for airlines.</summary>
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public TransportMode Mode { get; set; }
    public CarrierIntegration Integration { get; set; }
    /// <summary>UNB interchange id of the carrier when messages are exchanged as EDIFACT.</summary>
    public string? EdiPartnerId { get; set; }
    public string? EdiQualifier { get; set; }
    public string? ApiBaseUrl { get; set; }
    public string? TrackingUrlTemplate { get; set; }
    public bool Active { get; set; } = true;
    public decimal OnTimeTargetPercent { get; set; } = 95m;

    public string? TrackingUrl(string? reference) =>
        string.IsNullOrEmpty(TrackingUrlTemplate) || string.IsNullOrEmpty(reference)
            ? null
            : TrackingUrlTemplate.Replace("{ref}", Uri.EscapeDataString(reference), StringComparison.Ordinal);
}
