namespace Humongous.Quoting.Domain;

public enum ConstructionType
{
    Standard,
    TimberFrame,
    NonStandard
}

public enum RoofType
{
    Standard,
    FlatFelt,
    Thatch,
    Other
}

public enum AlarmType
{
    None,
    Audible,
    RemoteSignalling
}

public sealed record InsuredDetails
{
    public string Name { get; init; } = "";
    public string AddressLine1 { get; init; } = "";
    public string? AddressLine2 { get; init; }
    public string Town { get; init; } = "";
    public string Postcode { get; init; } = "";
    public string TradeCode { get; init; } = "";
    public int YearsTrading { get; init; }
    public decimal Turnover { get; init; }
    public string? ContactEmail { get; init; }
}

public sealed record PremisesDetails
{
    public ConstructionType Construction { get; init; } = ConstructionType.Standard;
    public RoofType Roof { get; init; } = RoofType.Standard;
    public int YearBuilt { get; init; }
    public bool Occupied { get; init; } = true;
    public bool ListedBuilding { get; init; }
    public bool FloodInLast10Years { get; init; }
    public AlarmType Alarm { get; init; } = AlarmType.None;
}

public sealed record MaterialDamageCover
{
    public decimal Buildings { get; init; }
    public decimal Contents { get; init; }
    public decimal Stock { get; init; }
    public decimal ComputerEquipment { get; init; }
    public decimal Excess { get; init; } = 250m;

    public decimal TotalSumInsured => Buildings + Contents + Stock + ComputerEquipment;
}

public sealed record BusinessInterruptionCover
{
    public decimal GrossProfit { get; init; }
    public int IndemnityPeriodMonths { get; init; } = 12;
}

public sealed record EmployersLiabilityCover
{
    public const decimal StandardLimit = 10_000_000m;

    public int Employees { get; init; }
    public decimal Wageroll { get; init; }
    public decimal ManualWageroll { get; init; }
}

public sealed record PublicLiabilityCover
{
    public static readonly decimal[] AvailableLimits = [1_000_000m, 2_000_000m, 5_000_000m];

    public decimal LimitOfIndemnity { get; init; } = 2_000_000m;
    public bool ProductsSupplied { get; init; }
    public bool WorkAwayFromPremises { get; init; }
}

public sealed record ClaimRecord
{
    public DateOnly Date { get; init; }
    public SectionCode Section { get; init; }
    public decimal Amount { get; init; }
    public string Description { get; init; } = "";
    public bool Open { get; init; }
}

/// <summary>The full risk presented by the broker for a quote.</summary>
public sealed record RiskDetails
{
    public InsuredDetails Insured { get; init; } = new();
    public PremisesDetails Premises { get; init; } = new();
    public MaterialDamageCover? MaterialDamage { get; init; }
    public BusinessInterruptionCover? BusinessInterruption { get; init; }
    public EmployersLiabilityCover? EmployersLiability { get; init; }
    public PublicLiabilityCover? PublicLiability { get; init; }
    public IReadOnlyList<ClaimRecord> Claims { get; init; } = [];
    public bool PreviouslyDeclinedOrCancelled { get; init; }

    public IEnumerable<SectionCode> SelectedSections()
    {
        if (MaterialDamage is not null) yield return SectionCode.MaterialDamage;
        if (BusinessInterruption is not null) yield return SectionCode.BusinessInterruption;
        if (EmployersLiability is not null) yield return SectionCode.EmployersLiability;
        if (PublicLiability is not null) yield return SectionCode.PublicLiability;
    }

    public decimal TotalPropertySumInsured =>
        (MaterialDamage?.TotalSumInsured ?? 0m) + (BusinessInterruption?.GrossProfit ?? 0m);
}
