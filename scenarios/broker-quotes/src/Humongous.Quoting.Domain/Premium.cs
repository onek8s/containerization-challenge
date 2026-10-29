namespace Humongous.Quoting.Domain;

public sealed record SectionPremium(
    SectionCode Section,
    decimal Technical,
    decimal MinimumApplied,
    decimal LoadingPercent,
    decimal Net,
    decimal Gross);

public sealed record PremiumBreakdown
{
    public IReadOnlyList<SectionPremium> Sections { get; init; } = [];
    public decimal NetPremium { get; init; }
    public decimal CommissionRate { get; init; }
    public decimal Commission { get; init; }
    public decimal GrossPremium { get; init; }
    public decimal IptRate { get; init; }
    public decimal Ipt { get; init; }
    public decimal Fee { get; init; }
    public decimal Total { get; init; }
}

/// <summary>
/// Turns the technical (net) premium from the rating tables into the premium the broker sees:
/// section minimums, underwriter loadings, gross up for commission, insurance premium tax and fee.
/// </summary>
public static class PremiumCalculator
{
    public static PremiumBreakdown Calculate(
        RatingResult rating,
        ProductVersion product,
        decimal commissionRate,
        IReadOnlyDictionary<SectionCode, decimal>? loadings = null,
        bool chargeFee = true)
    {
        if (commissionRate < 0 || commissionRate > product.MaxCommission)
            throw new ArgumentOutOfRangeException(nameof(commissionRate),
                $"Commission must be between 0 and {product.MaxCommission:P0}.");

        var sections = new List<SectionPremium>();
        foreach (var s in rating.Sections)
        {
            var minimum = product.MinimumFor(s.Section);
            var afterMinimum = Math.Max(s.Premium, minimum);
            var loading = loadings is not null && loadings.TryGetValue(s.Section, out var l) ? l : 0m;
            var net = Money.Round(afterMinimum * (1 + loading / 100m));
            var gross = Money.Round(net / (1 - commissionRate));
            sections.Add(new SectionPremium(s.Section, Money.Round(s.Premium),
                afterMinimum > s.Premium ? Money.Round(minimum - s.Premium) : 0m, loading, net, gross));
        }

        var netPremium = sections.Sum(s => s.Net);
        var grossPremium = sections.Sum(s => s.Gross);
        var commission = grossPremium - netPremium;
        var ipt = Money.Round(grossPremium * product.IptRate);
        var fee = chargeFee ? product.PolicyFee : 0m;

        return new PremiumBreakdown
        {
            Sections = sections,
            NetPremium = netPremium,
            CommissionRate = commissionRate,
            Commission = commission,
            GrossPremium = grossPremium,
            IptRate = product.IptRate,
            Ipt = ipt,
            Fee = fee,
            Total = grossPremium + ipt + fee
        };
    }
}

/// <summary>Pro rata premium for mid-term changes and cancellations.</summary>
public static class ProRata
{
    public static decimal Factor(DateOnly periodStart, DateOnly periodEnd, DateOnly effective)
    {
        if (effective < periodStart || effective > periodEnd)
            throw new ArgumentOutOfRangeException(nameof(effective), "Effective date is outside the period of insurance.");

        var periodDays = periodEnd.DayNumber - periodStart.DayNumber + 1;
        var remaining = periodEnd.DayNumber - effective.DayNumber + 1;
        return (decimal)remaining / periodDays;
    }

    /// <summary>Additional (positive) or return (negative) premium for a change effective mid-term.</summary>
    public static decimal Adjustment(decimal oldAnnual, decimal newAnnual, DateOnly periodStart, DateOnly periodEnd, DateOnly effective) =>
        Money.Round((newAnnual - oldAnnual) * Factor(periodStart, periodEnd, effective));

    /// <summary>Return premium on cancellation. Within the first 14 days the full premium is returned.</summary>
    public static decimal CancellationReturn(decimal annualGross, DateOnly periodStart, DateOnly periodEnd, DateOnly effective)
    {
        if (effective.DayNumber - periodStart.DayNumber < 14)
            return annualGross;
        return Money.Round(annualGross * Factor(periodStart, periodEnd, effective.AddDays(1)));
    }
}
