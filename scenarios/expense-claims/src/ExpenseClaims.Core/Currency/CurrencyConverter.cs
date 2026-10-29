namespace ExpenseClaims.Core.Currency;

/// <summary>Reference rate: how many units of <see cref="Currency"/> one euro buys on <see cref="Date"/>.</summary>
public sealed record ReferenceRate(DateOnly Date, string Currency, decimal UnitsPerEuro);

public static class CurrencyConverter
{
    /// <summary>Rates older than this are not used for a conversion.</summary>
    public const int MaxRateAgeDays = 7;

    /// <summary>
    /// Picks the latest rate published on or before the expense date. Weekends and TARGET holidays
    /// have no rate, so the previous business day's rate applies.
    /// </summary>
    public static ReferenceRate? PickRate(IEnumerable<ReferenceRate> rates, string currency, DateOnly expenseDate) =>
        rates.Where(r => string.Equals(r.Currency, currency, StringComparison.OrdinalIgnoreCase)
                         && r.Date <= expenseDate
                         && r.Date >= expenseDate.AddDays(-MaxRateAgeDays))
             .OrderByDescending(r => r.Date)
             .FirstOrDefault();

    public static decimal ToEuro(decimal amount, decimal unitsPerEuro)
    {
        if (unitsPerEuro <= 0)
            throw new ArgumentOutOfRangeException(nameof(unitsPerEuro));
        return Math.Round(amount / unitsPerEuro, 2, MidpointRounding.AwayFromZero);
    }
}
