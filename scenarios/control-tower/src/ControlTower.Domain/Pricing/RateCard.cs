namespace Proseware.ControlTower.Domain.Pricing;

/// <summary>Purchase or sales rate for a lane (country to country) and mode.</summary>
public class RateCard
{
    public int Id { get; set; }
    public int? CarrierId { get; set; }
    public TransportMode Mode { get; set; }
    public string OriginCountry { get; set; } = "";
    public string DestinationCountry { get; set; } = "";
    public string? OriginPostalPrefix { get; set; }
    public string Currency { get; set; } = "EUR";
    public decimal MinimumCharge { get; set; }
    /// <summary>Price per 100 kg of chargeable weight, by weight break.</summary>
    public List<RateBreak> Breaks { get; set; } = new();
    public decimal FuelSurchargePercent { get; set; }
    public int TransitDays { get; set; }
    public DateTime ValidFrom { get; set; }
    public DateTime ValidTo { get; set; }

    public bool AppliesTo(TransportMode mode, string from, string to, string? postalCode, DateTime date) =>
        Mode == mode
        && string.Equals(OriginCountry, from, StringComparison.OrdinalIgnoreCase)
        && string.Equals(DestinationCountry, to, StringComparison.OrdinalIgnoreCase)
        && (OriginPostalPrefix is null || (postalCode?.StartsWith(OriginPostalPrefix, StringComparison.Ordinal) ?? false))
        && date >= ValidFrom && date <= ValidTo;
}

public class RateBreak
{
    public int Id { get; set; }
    public int RateCardId { get; set; }
    public decimal FromKg { get; set; }
    public decimal PricePer100Kg { get; set; }
}
