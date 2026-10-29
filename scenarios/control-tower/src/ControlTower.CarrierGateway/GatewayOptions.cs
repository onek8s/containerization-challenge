namespace Proseware.ControlTower.CarrierGateway;

public sealed class GatewayOptions
{
    public const string Section = "Gateway";

    /// <summary>Local copies of every sent and received file, kept for the EDI help desk.</summary>
    public string WorkFolder { get; set; } = @"D:\ControlTower\Edi";
    public int EdiPollSeconds { get; set; } = 60;
    public int TrackingPollSeconds { get; set; } = 300;
    /// <summary>Webhook listener, e.g. "http://*:8085".</summary>
    public string WebhookUrls { get; set; } = "http://*:8085";
    public bool EdiEnabled { get; set; } = true;
    public bool TrackingEnabled { get; set; } = true;
    public bool CustomsEnabled { get; set; } = true;

    public string Folder(params string[] parts)
    {
        var path = Path.Combine(new[] { WorkFolder }.Concat(parts).ToArray());
        Directory.CreateDirectory(path);
        return path;
    }
}
