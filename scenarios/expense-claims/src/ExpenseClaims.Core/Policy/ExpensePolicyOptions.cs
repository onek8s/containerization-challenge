namespace ExpenseClaims.Core.Policy;

/// <summary>
/// Travel and expense policy (HR-POL-07). Rates follow the Finnish Tax Administration decision
/// for the year; finance updates the Policy section of the API configuration each January.
/// </summary>
public sealed class ExpensePolicyOptions
{
    public const string SectionName = "Policy";

    public string BaseCurrency { get; set; } = "EUR";

    /// <summary>Receipt is required for any line at or above this amount (base currency).</summary>
    public decimal ReceiptRequiredFrom { get; set; } = 25m;

    public decimal MileageRatePerKm { get; set; } = 0.46m;
    public decimal MaxKilometresPerLine { get; set; } = 2000m;

    public decimal PerDiemFull { get; set; } = 48m;
    public decimal PerDiemPartial { get; set; } = 22m;

    /// <summary>Daily allowance abroad by ISO country code. Countries not listed use <see cref="PerDiemAbroadDefault"/>.</summary>
    public Dictionary<string, decimal> PerDiemAbroad { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public decimal PerDiemAbroadDefault { get; set; } = 60m;

    public decimal AccommodationPerNight { get; set; } = 180m;
    public decimal MealsPerPersonLimit { get; set; } = 60m;
    public decimal RepresentationPerPersonLimit { get; set; } = 90m;
    public decimal EquipmentLimit { get; set; } = 300m;

    /// <summary>Lines older than this many days cannot be claimed.</summary>
    public int MaxAgeDays { get; set; } = 90;

    /// <summary>Claims with a total at or above this amount also need the approver's own manager.</summary>
    public decimal SecondApprovalFrom { get; set; } = 2500m;
}
