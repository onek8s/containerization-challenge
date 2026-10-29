using CityPermits.Domain.Model;

namespace CityPermits.Domain.Fees;

public sealed record FeeQuote(IReadOnlyList<FeeLine> Lines)
{
    public decimal Total => Lines.Where(l => !l.Waived).Sum(l => l.Amount);
}

public sealed record FloorAreaBand(double UpToM2, decimal Fee);
public sealed record CostBand(decimal UpTo, decimal Fixed, decimal PerThousandAbove, decimal Above);

/// <summary>
/// Fees and charges 2024/25 as adopted by Council (minute 2024/031). Values are loaded from the
/// FeeSchedule table each year; the defaults below are the 2024/25 schedule.
/// </summary>
public sealed class FeeSchedule
{
    public int Year { get; init; } = 2024;
    public decimal VatRate { get; init; } = 0m;

    public IReadOnlyList<FloorAreaBand> ExtensionBands { get; init; } = new[]
    {
        new FloorAreaBand(10, 240m), new FloorAreaBand(40, 410m), new FloorAreaBand(60, 560m),
        new FloorAreaBand(100, 790m), new FloorAreaBand(double.MaxValue, 1020m)
    };

    public IReadOnlyList<CostBand> NewBuildBands { get; init; } = new[]
    {
        new CostBand(100_000m, 950m, 0m, 0m),
        new CostBand(250_000m, 950m, 4.20m, 100_000m),
        new CostBand(1_000_000m, 1580m, 3.10m, 250_000m),
        new CostBand(decimal.MaxValue, 3905m, 1.75m, 1_000_000m)
    };

    public decimal AlterationFee { get; init; } = 320m;
    public decimal DemolitionFee { get; init; } = 285m;
    public decimal OutbuildingFee { get; init; } = 210m;
    public decimal SignageFee { get; init; } = 160m;
    public decimal ChangeOfUseFee { get; init; } = 640m;
    public decimal HeritageSurcharge { get; init; } = 180m;
    public decimal FloodAssessmentFee { get; init; } = 145m;
    public decimal InspectionVisitFee { get; init; } = 95m;
    public decimal FastTrackSurcharge { get; init; } = 0.35m;

    public IReadOnlyDictionary<BusinessCategory, decimal> LicenceAnnual { get; init; } = new Dictionary<BusinessCategory, decimal>
    {
        [BusinessCategory.FoodPremises] = 215m,
        [BusinessCategory.AlcoholOnSale] = 470m,
        [BusinessCategory.AlcoholOffSale] = 300m,
        [BusinessCategory.TaxiVehicle] = 188m,
        [BusinessCategory.StreetTrading] = 640m,
        [BusinessCategory.Entertainment] = 890m,
        [BusinessCategory.ScrapMetalDealer] = 395m,
        [BusinessCategory.HairAndBeauty] = 120m,
        [BusinessCategory.AnimalBoarding] = 260m,
    };

    public decimal CapacitySurchargePer100 { get; init; } = 85m;
    public decimal SeasonalDiscount { get; init; } = 0.4m;
    public decimal NewApplicationAdminFee { get; init; } = 55m;

    public decimal SecondResidentPermitMultiplier { get; init; } = 1.5m;
    public decimal ThirdResidentPermitMultiplier { get; init; } = 2.25m;
    public IReadOnlyDictionary<VehicleEmissionBand, decimal> EmissionMultiplier { get; init; } = new Dictionary<VehicleEmissionBand, decimal>
    {
        [VehicleEmissionBand.Zero] = 0.25m,
        [VehicleEmissionBand.Low] = 0.75m,
        [VehicleEmissionBand.Standard] = 1.0m,
        [VehicleEmissionBand.High] = 1.4m,
    };
    public decimal CarerPermitFee { get; init; } = 0m;
    public decimal MinimumParkingCharge { get; init; } = 12m;
}
