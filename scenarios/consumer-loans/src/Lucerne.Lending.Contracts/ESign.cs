namespace Lucerne.Lending.Contracts;

/// <summary>Callback posted by the e-signature provider when a signing order changes state.</summary>
public sealed class SigningCallback
{
    public string OrderId { get; set; } = "";
    public string ExternalReference { get; set; } = "";
    public string State { get; set; } = "";
    public DateTime Timestamp { get; set; }
    public string? Signature { get; set; }
}
