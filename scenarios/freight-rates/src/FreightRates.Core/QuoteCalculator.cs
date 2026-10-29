namespace FreightRates.Core;

public sealed record CustomerPricing(decimal DiscountPercent, decimal MinimumCharge, decimal? FuelSurchargeOverridePercent = null);

public sealed record AccessorialRate(string Code, string Description, decimal Amount, decimal? PerCwt = null);

public sealed record AccessorialLine(string Code, string Description, decimal Amount);

public sealed record QuoteBreakdown
{
    public decimal GrossCharge { get; init; }
    public decimal Discount { get; init; }
    public decimal NetLinehaul { get; init; }
    public bool MinimumApplied { get; init; }
    public decimal FuelSurchargePercent { get; init; }
    public decimal FuelSurcharge { get; init; }
    public IReadOnlyList<AccessorialLine> Accessorials { get; init; } = Array.Empty<AccessorialLine>();
    public decimal AccessorialTotal { get; init; }
    public decimal Total { get; init; }
}

public static class QuoteCalculator
{
    public static QuoteBreakdown Calculate(
        EngineRateResult rate,
        CustomerPricing pricing,
        decimal fuelSurchargePercent,
        int totalWeightLbs,
        IEnumerable<AccessorialRate> accessorials)
    {
        if (pricing.DiscountPercent is < 0 or > 95)
            throw new ArgumentOutOfRangeException(nameof(pricing), "Discount must be between 0 and 95 percent.");

        var gross = rate.GrossCharge;
        var discount = Money(gross * pricing.DiscountPercent / 100m);
        var net = gross - discount;

        var minimum = pricing.MinimumCharge > 0 ? pricing.MinimumCharge : rate.TariffMinimumCharge;
        var minimumApplied = net < minimum;
        if (minimumApplied)
            net = minimum;

        var fuelPercent = pricing.FuelSurchargeOverridePercent ?? fuelSurchargePercent;
        var fuel = Money(net * fuelPercent / 100m);

        var cwt = totalWeightLbs / 100m;
        var lines = accessorials
            .Select(a => new AccessorialLine(
                a.Code,
                a.Description,
                a.PerCwt is { } perCwt ? Math.Max(a.Amount, Money(perCwt * cwt)) : a.Amount))
            .ToList();
        var accessorialTotal = lines.Sum(l => l.Amount);

        return new QuoteBreakdown
        {
            GrossCharge = gross,
            Discount = discount,
            NetLinehaul = net,
            MinimumApplied = minimumApplied,
            FuelSurchargePercent = fuelPercent,
            FuelSurcharge = fuel,
            Accessorials = lines,
            AccessorialTotal = accessorialTotal,
            Total = net + fuel + accessorialTotal,
        };
    }

    public static decimal Money(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
