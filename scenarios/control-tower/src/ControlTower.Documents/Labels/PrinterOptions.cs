namespace Proseware.ControlTower.Documents.Labels;

public enum PrinterKind
{
    /// <summary>Zebra printer reachable over the network (raw TCP, port 9100).</summary>
    Network = 0,

    /// <summary>Writes the label into a folder (training and test environments).</summary>
    File = 1
}

public sealed class PrinterDefinition
{
    public PrinterKind Kind { get; set; }

    /// <summary>"host:port" for network printers, folder path for file printers.</summary>
    public string Address { get; set; } = "";

    public string? Terminal { get; set; }

    /// <summary>Print resolution in dots per mm: 8 (203 dpi) or 12 (300 dpi).</summary>
    public int DotsPerMm { get; set; } = 8;
}

public sealed class PrinterOptions
{
    public const string Section = "Labels";

    public Dictionary<string, PrinterDefinition> Printers { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public int SendTimeoutSeconds { get; set; } = 10;

    public int Retries { get; set; } = 2;
}
