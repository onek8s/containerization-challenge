namespace FreightRates.Core;

public sealed record ShipmentLine
{
    /// <summary>NMFC freight class, e.g. 77.5. Optional when dimensions are given.</summary>
    public decimal? FreightClass { get; init; }
    public int WeightLbs { get; init; }
    public int Pieces { get; init; } = 1;
    public string? Description { get; init; }
    public decimal? LengthIn { get; init; }
    public decimal? WidthIn { get; init; }
    public decimal? HeightIn { get; init; }
}

public sealed record ShipmentRequest
{
    public required string OriginZip { get; init; }
    public required string DestinationZip { get; init; }
    public DateOnly ShipDate { get; init; }
    public IReadOnlyList<ShipmentLine> Lines { get; init; } = Array.Empty<ShipmentLine>();
    public IReadOnlyList<string> Accessorials { get; init; } = Array.Empty<string>();

    public int TotalWeightLbs => Lines.Sum(l => l.WeightLbs);
}
