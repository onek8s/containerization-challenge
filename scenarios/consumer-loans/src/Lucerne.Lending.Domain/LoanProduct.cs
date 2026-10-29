namespace Lucerne.Lending.Domain;

public enum LoanPurpose
{
    Other = 0,
    Car = 1,
    Renovation = 2,
    Consolidation = 3,
    Travel = 4,
    Studies = 5,
    Household = 6
}

public enum ReferenceRateType
{
    Fixed = 0,
    Euribor3M = 3,
    Euribor12M = 12
}

/// <summary>Loan product as maintained by the product team (table lending.Product).</summary>
public sealed class LoanProduct
{
    public string Code { get; init; } = "";
    public string Name { get; init; } = "";
    public string CoreProductCode { get; init; } = "";
    public decimal MinAmount { get; init; }
    public decimal MaxAmount { get; init; }
    public int MinTermMonths { get; init; }
    public int MaxTermMonths { get; init; }
    public ReferenceRateType ReferenceRate { get; init; }
    /// <summary>Margin in percent for risk grade A; grades add <see cref="Pricing.GradeAddOn"/>.</summary>
    public decimal BaseMarginPercent { get; init; }
    public decimal ArrangementFee { get; init; }
    public decimal MonthlyFee { get; init; }
    public bool RequiresCollateral { get; init; }
    public bool AllowsCoApplicant { get; init; } = true;
    public LoanPurpose[] Purposes { get; init; } = Array.Empty<LoanPurpose>();
    public bool Active { get; init; } = true;

    public IEnumerable<string> Validate(decimal amount, int termMonths, LoanPurpose purpose)
    {
        if (amount < MinAmount || amount > MaxAmount)
            yield return $"Amount must be between {MinAmount:0} and {MaxAmount:0} EUR.";
        if (termMonths < MinTermMonths || termMonths > MaxTermMonths)
            yield return $"Term must be between {MinTermMonths} and {MaxTermMonths} months.";
        if (Purposes.Length > 0 && !Purposes.Contains(purpose))
            yield return $"Purpose {purpose} is not available for {Name}.";
    }
}
