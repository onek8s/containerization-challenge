namespace Humongous.Quoting.Domain;

/// <summary>
/// A released version of a product. The rating tables for the version live in RateCore;
/// this record holds the commercial parameters applied around the technical premium.
/// </summary>
public sealed record ProductVersion
{
    public string ProductCode { get; init; } = "BC";
    public string Version { get; init; } = "";
    public string Name { get; init; } = "Business Combined";
    public DateOnly EffectiveFrom { get; init; }
    public DateOnly? EffectiveTo { get; init; }
    public string RateCoreProduct { get; init; } = "BC";
    public decimal IptRate { get; init; } = 0.12m;
    public decimal MaxCommission { get; init; } = 0.25m;
    public decimal PolicyFee { get; init; } = 25m;
    public decimal MaxTotalSumInsured { get; init; } = 5_000_000m;
    public IReadOnlyDictionary<SectionCode, decimal> SectionMinimums { get; init; } =
        new Dictionary<SectionCode, decimal>();

    public decimal MinimumFor(SectionCode section) =>
        SectionMinimums.TryGetValue(section, out var min) ? min : 0m;

    public bool IsEffectiveOn(DateOnly date) =>
        date >= EffectiveFrom && (EffectiveTo is null || date <= EffectiveTo);
}

public static class ProductCatalogue
{
    public static readonly IReadOnlyList<ProductVersion> Versions =
    [
        new ProductVersion
        {
            Version = "2023.2",
            EffectiveFrom = new DateOnly(2023, 7, 1),
            EffectiveTo = new DateOnly(2024, 3, 31),
            IptRate = 0.12m,
            PolicyFee = 20m,
            SectionMinimums = new Dictionary<SectionCode, decimal>
            {
                [SectionCode.MaterialDamage] = 150m,
                [SectionCode.BusinessInterruption] = 75m,
                [SectionCode.EmployersLiability] = 125m,
                [SectionCode.PublicLiability] = 100m
            }
        },
        new ProductVersion
        {
            Version = "2024.1",
            EffectiveFrom = new DateOnly(2024, 4, 1),
            EffectiveTo = null,
            IptRate = 0.12m,
            PolicyFee = 25m,
            SectionMinimums = new Dictionary<SectionCode, decimal>
            {
                [SectionCode.MaterialDamage] = 175m,
                [SectionCode.BusinessInterruption] = 85m,
                [SectionCode.EmployersLiability] = 140m,
                [SectionCode.PublicLiability] = 110m
            }
        }
    ];

    public static ProductVersion ForDate(DateOnly inception) =>
        Versions.LastOrDefault(v => v.IsEffectiveOn(inception))
        ?? throw new InvalidOperationException($"No Business Combined version is effective on {inception:yyyy-MM-dd}.");

    public static ProductVersion? Find(string version) =>
        Versions.FirstOrDefault(v => v.Version == version);
}
