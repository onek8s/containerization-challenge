using Humongous.Quoting.Domain;

namespace Humongous.Quoting.Tests;

internal static class TestRisks
{
    public static ProductVersion Product => ProductCatalogue.Find("2024.1")!;

    public static RiskDetails Cafe() => new()
    {
        Insured = new InsuredDetails
        {
            Name = "Kettle & Crumb Ltd",
            AddressLine1 = "14 Briggate",
            Town = "Leeds",
            Postcode = "ls1 6hd",
            TradeCode = "56102",
            YearsTrading = 6,
            Turnover = 420_000m
        },
        Premises = new PremisesDetails { YearBuilt = 1965, Alarm = AlarmType.Audible },
        MaterialDamage = new MaterialDamageCover { Contents = 60_000m, Stock = 8_000m, ComputerEquipment = 4_000m },
        BusinessInterruption = new BusinessInterruptionCover { GrossProfit = 150_000m, IndemnityPeriodMonths = 12 },
        EmployersLiability = new EmployersLiabilityCover { Employees = 9, Wageroll = 180_000m, ManualWageroll = 120_000m },
        PublicLiability = new PublicLiabilityCover { LimitOfIndemnity = 2_000_000m, ProductsSupplied = true }
    };

    public static RatingResult Rated(params (SectionCode Section, decimal Premium)[] sections) => new()
    {
        EngineVersion = "test",
        TableVersion = "BC-2024.1",
        Sections = sections.Select(s => new SectionRating(s.Section, s.Premium, 1m, "test")).ToList()
    };
}
