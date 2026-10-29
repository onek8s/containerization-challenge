namespace ExpenseClaims.Core.Accounting;

public sealed class AccountingOptions
{
    public const string SectionName = "Accounting";

    public string HomeCountry { get; set; } = "FI";

    /// <summary>Deductible VAT rate (percent) by expense category. Categories not listed have no deductible VAT.</summary>
    public Dictionary<string, decimal> VatRates { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Travel"] = 10m,
        ["Accommodation"] = 10m,
        ["Training"] = 24m,
        ["Equipment"] = 24m,
        ["Other"] = 24m
    };

    /// <summary>G/L expense account by expense category.</summary>
    public Dictionary<string, string> ExpenseAccounts { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Travel"] = "7610",
        ["Accommodation"] = "7620",
        ["Meals"] = "7630",
        ["Mileage"] = "7640",
        ["PerDiem"] = "7650",
        ["Training"] = "7700",
        ["Equipment"] = "7810",
        ["Representation"] = "7910",
        ["Other"] = "7990"
    };

    public string VatReceivableAccount { get; set; } = "2930";

    /// <summary>Liability to employees, cleared by the bank payment.</summary>
    public string EmployeePayableAccount { get; set; } = "2870";

    /// <summary>Company card clearing account, cleared by the card company's monthly invoice.</summary>
    public string CardClearingAccount { get; set; } = "2875";
}
