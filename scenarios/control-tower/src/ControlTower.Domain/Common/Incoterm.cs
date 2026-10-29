namespace Proseware.ControlTower.Domain;

/// <summary>Incoterms 2020 and who arranges and pays which part of the carriage.</summary>
public sealed record Incoterm(string Code, string Name, bool SellerPaysMainCarriage, bool SellerClearsImport, bool SellerClearsExport, bool SellerInsures, bool AnyMode)
{
    public static readonly Incoterm EXW = new("EXW", "Ex Works", false, false, false, false, true);
    public static readonly Incoterm FCA = new("FCA", "Free Carrier", false, false, true, false, true);
    public static readonly Incoterm CPT = new("CPT", "Carriage Paid To", true, false, true, false, true);
    public static readonly Incoterm CIP = new("CIP", "Carriage and Insurance Paid To", true, false, true, true, true);
    public static readonly Incoterm DAP = new("DAP", "Delivered at Place", true, false, true, false, true);
    public static readonly Incoterm DPU = new("DPU", "Delivered at Place Unloaded", true, false, true, false, true);
    public static readonly Incoterm DDP = new("DDP", "Delivered Duty Paid", true, true, true, false, true);
    public static readonly Incoterm FAS = new("FAS", "Free Alongside Ship", false, false, true, false, false);
    public static readonly Incoterm FOB = new("FOB", "Free on Board", false, false, true, false, false);
    public static readonly Incoterm CFR = new("CFR", "Cost and Freight", true, false, true, false, false);
    public static readonly Incoterm CIF = new("CIF", "Cost, Insurance and Freight", true, false, true, true, false);

    public static IReadOnlyList<Incoterm> All { get; } = new[] { EXW, FCA, CPT, CIP, DAP, DPU, DDP, FAS, FOB, CFR, CIF };

    public static Incoterm Parse(string code) =>
        All.FirstOrDefault(i => string.Equals(i.Code, code?.Trim(), StringComparison.OrdinalIgnoreCase))
        ?? throw new ArgumentException($"Unknown Incoterm '{code}'.", nameof(code));

    public static bool TryParse(string? code, out Incoterm incoterm)
    {
        incoterm = All.FirstOrDefault(i => string.Equals(i.Code, code?.Trim(), StringComparison.OrdinalIgnoreCase))!;
        return incoterm is not null;
    }

    /// <summary>Sea-only terms cannot be used for road or air bookings.</summary>
    public bool IsValidFor(TransportMode mode) => AnyMode || mode == TransportMode.Sea;

    public override string ToString() => Code;
}
