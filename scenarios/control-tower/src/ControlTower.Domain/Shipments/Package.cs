namespace Proseware.ControlTower.Domain.Shipments;

public class Package
{
    public int Id { get; set; }
    public int ShipmentId { get; set; }
    /// <summary>Serial Shipping Container Code (18 digits, GS1 check digit), printed on the label.</summary>
    public string Sscc { get; set; } = "";
    public PackageKind Kind { get; set; }
    public int Quantity { get; set; } = 1;
    public decimal WeightKg { get; set; }
    public int LengthCm { get; set; }
    public int WidthCm { get; set; }
    public int HeightCm { get; set; }
    public bool Stackable { get; set; } = true;
    public string? Marks { get; set; }
    public DangerousGoods? DangerousGoods { get; set; }
    /// <summary>ISO 6346 container number for full container loads.</summary>
    public string? ContainerNumber { get; set; }
    public string? SealNumber { get; set; }

    public decimal VolumeM3 => Math.Round(LengthCm * WidthCm * HeightCm / 1_000_000m, 3);

    /// <summary>Loading metres on a 2.4 m wide trailer; non-stackable goods use full height.</summary>
    public decimal LoadingMeters
    {
        get
        {
            if (Kind is PackageKind.Container20 or PackageKind.Container40 or PackageKind.Container40HighCube) return 0;
            var floor = LengthCm / 100m * (WidthCm / 100m) / 2.4m;
            return Math.Round(floor * Quantity / (Stackable ? 2 : 1), 2);
        }
    }
}

/// <summary>ADR / IMDG / IATA dangerous goods details per UN number.</summary>
public class DangerousGoods
{
    public string UnNumber { get; set; } = "";
    public string ProperShippingName { get; set; } = "";
    /// <summary>Hazard class, e.g. "3", "2.1", "8".</summary>
    public string HazardClass { get; set; } = "";
    public string? PackingGroup { get; set; }
    public decimal NetQuantity { get; set; }
    public string NetQuantityUnit { get; set; } = "kg";
    public bool LimitedQuantity { get; set; }
    public string? TunnelCode { get; set; }
}
