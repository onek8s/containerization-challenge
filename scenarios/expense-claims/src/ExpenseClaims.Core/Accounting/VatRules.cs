using ExpenseClaims.Core.Claims;

namespace ExpenseClaims.Core.Accounting;

/// <summary>
/// Deductible VAT included in domestic expenses (Finnish VAT act). Foreign expenses, mileage and daily
/// allowances carry no deductible VAT; meals and representation are not deductible.
/// </summary>
public sealed class VatRules
{
    private readonly AccountingOptions _options;

    public VatRules(AccountingOptions options) => _options = options;

    public decimal RateFor(ExpenseCategory category, string currency, string? country)
    {
        if (!string.Equals(currency, "EUR", StringComparison.OrdinalIgnoreCase))
            return 0m;
        if (country is not null && !string.Equals(country, _options.HomeCountry, StringComparison.OrdinalIgnoreCase))
            return 0m;
        return _options.VatRates.TryGetValue(category.ToString(), out var rate) ? rate : 0m;
    }

    /// <summary>VAT part of a gross amount.</summary>
    public static decimal VatOf(decimal gross, decimal ratePercent) =>
        ratePercent <= 0 ? 0m : Math.Round(gross * ratePercent / (100m + ratePercent), 2, MidpointRounding.AwayFromZero);
}
