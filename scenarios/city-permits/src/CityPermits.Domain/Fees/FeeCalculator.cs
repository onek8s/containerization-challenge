using CityPermits.Domain.Model;

namespace CityPermits.Domain.Fees;

public sealed class FeeCalculator
{
    private readonly FeeSchedule _schedule;

    public FeeCalculator(FeeSchedule schedule) => _schedule = schedule;

    public FeeQuote Calculate(PermitCase c, SiteConstraints? site, ZoneTariff? tariff, bool fastTrack = false, DateTime? nowUtc = null)
    {
        var now = nowUtc ?? DateTime.UtcNow;
        var lines = c.Kind switch
        {
            PermitKind.BuildingPermit => Building(c, site, fastTrack),
            PermitKind.BusinessLicence => Licence(c),
            PermitKind.ParkingPermit => Parking(c, tariff ?? throw new InvalidOperationException("Parking zone tariff is required.")),
            _ => throw new NotSupportedException(c.Kind.ToString())
        };
        foreach (var l in lines)
        {
            l.Amount = Money.Round(l.Amount);
            l.VatRate = _schedule.VatRate;
            l.CreatedUtc = now;
        }
        return new FeeQuote(lines);
    }

    private List<FeeLine> Building(PermitCase c, SiteConstraints? site, bool fastTrack)
    {
        var b = c.Building ?? throw new InvalidOperationException("Building details are missing.");
        var lines = new List<FeeLine>();
        switch (b.WorkType)
        {
            case WorkType.NewBuilding:
                lines.Add(Line("BP-NEW", $"New building, estimated cost {b.EstimatedCost:N0}", CostBased(b.EstimatedCost)));
                break;
            case WorkType.Extension:
                var band = _schedule.ExtensionBands.First(x => b.GrossFloorAreaM2 <= x.UpToM2);
                lines.Add(Line("BP-EXT", $"Extension {b.GrossFloorAreaM2:0.#} m²", band.Fee));
                break;
            case WorkType.Alteration:
                lines.Add(Line("BP-ALT", "Internal or structural alteration", _schedule.AlterationFee));
                break;
            case WorkType.Demolition:
                lines.Add(Line("BP-DEM", "Demolition", _schedule.DemolitionFee));
                break;
            case WorkType.ChangeOfUse:
                lines.Add(Line("BP-COU", $"Change of use to {b.UseClass}", _schedule.ChangeOfUseFee));
                break;
            case WorkType.Outbuilding:
                lines.Add(Line("BP-OUT", "Outbuilding or garage", _schedule.OutbuildingFee));
                break;
            case WorkType.Signage:
                lines.Add(Line("BP-SGN", "Advertisement or signage", _schedule.SignageFee));
                break;
        }

        if (site is not null)
        {
            if (site.HeritageAreas.Count > 0 || b.ListedBuilding)
                lines.Add(Line("BP-HER", "Heritage assessment", _schedule.HeritageSurcharge));
            if (site.FloodZone is "2" or "3" or "3a" or "3b")
                lines.Add(Line("BP-FLD", $"Flood risk review (zone {site.FloodZone})", _schedule.FloodAssessmentFee));
        }

        var visits = InspectionPlanner.StagesFor(c).Count;
        if (visits > 0)
            lines.Add(Line("BP-INS", $"Site inspections ({visits} visits)", visits * _schedule.InspectionVisitFee));

        if (fastTrack)
        {
            var baseFee = lines.Sum(l => l.Amount);
            lines.Add(Line("BP-FST", "Fast-track decision", baseFee * _schedule.FastTrackSurcharge));
        }
        return lines;
    }

    public decimal CostBased(decimal estimatedCost)
    {
        var band = _schedule.NewBuildBands.First(x => estimatedCost <= x.UpTo);
        var above = Math.Max(0m, estimatedCost - band.Above);
        return band.Fixed + Math.Ceiling(above / 1000m) * band.PerThousandAbove;
    }

    private List<FeeLine> Licence(PermitCase c)
    {
        var b = c.Business ?? throw new InvalidOperationException("Business details are missing.");
        var lines = new List<FeeLine>();
        var annual = _schedule.LicenceAnnual[b.Category];
        if (b.Seasonal) annual *= 1 - _schedule.SeasonalDiscount;
        lines.Add(Line("BL-" + Code(b.Category), $"{Display(b.Category)} licence, 12 months{(b.Seasonal ? " (seasonal)" : "")}", annual));
        if (b.Capacity is > 100 && b.Category is BusinessCategory.AlcoholOnSale or BusinessCategory.Entertainment)
        {
            var extra = (int)Math.Ceiling((b.Capacity.Value - 100) / 100.0);
            lines.Add(Line("BL-CAP", $"Capacity surcharge ({b.Capacity} persons)", extra * _schedule.CapacitySurchargePer100));
        }
        if (c.RenewalOfCaseId is null)
            lines.Add(Line("BL-ADM", "New application administration", _schedule.NewApplicationAdminFee));
        return lines;
    }

    private List<FeeLine> Parking(PermitCase c, ZoneTariff tariff)
    {
        var p = c.Parking ?? throw new InvalidOperationException("Parking details are missing.");
        var lines = new List<FeeLine>();
        switch (p.PermitType)
        {
            case ParkingPermitType.Resident:
                var annual = tariff.ResidentAnnual * _schedule.EmissionMultiplier[p.EmissionBand];
                annual *= p.PermitNumberInHousehold switch
                {
                    1 => 1m,
                    2 => _schedule.SecondResidentPermitMultiplier,
                    _ => _schedule.ThirdResidentPermitMultiplier
                };
                var amount = Math.Max(_schedule.MinimumParkingCharge, annual * p.DurationMonths / 12m);
                lines.Add(Line("PP-RES", $"Resident permit zone {tariff.ZoneCode}, {p.DurationMonths} months, band {p.EmissionBand}", amount));
                break;
            case ParkingPermitType.Business:
                lines.Add(Line("PP-BUS", $"Business permit zone {tariff.ZoneCode}, {p.DurationMonths} months",
                    Math.Max(_schedule.MinimumParkingCharge, tariff.BusinessAnnual * p.DurationMonths / 12m)));
                break;
            case ParkingPermitType.Visitor:
                lines.Add(Line("PP-VIS", $"Visitor vouchers zone {tariff.ZoneCode}, book of 20", tariff.VisitorBookOf20));
                break;
            case ParkingPermitType.Carer:
                lines.Add(Line("PP-CAR", "Carer permit", _schedule.CarerPermitFee));
                break;
        }
        return lines;
    }

    /// <summary>Refund for surrendering a parking permit early: whole unused months, less an admin charge.</summary>
    public decimal SurrenderRefund(PermitCase c, DateOnly surrenderDate, decimal adminCharge = 10m)
    {
        if (c.ValidFrom is null || c.ValidTo is null) return 0m;
        var paid = c.Fees.Where(f => !f.Waived).Sum(f => f.Amount);
        var totalMonths = MonthsBetween(c.ValidFrom.Value, c.ValidTo.Value);
        var unused = MonthsBetween(surrenderDate, c.ValidTo.Value);
        if (totalMonths <= 0 || unused <= 0) return 0m;
        return Math.Max(0m, Money.Round(paid * unused / totalMonths - adminCharge));
    }

    private static int MonthsBetween(DateOnly from, DateOnly to)
    {
        var months = (to.Year - from.Year) * 12 + to.Month - from.Month;
        if (to.Day < from.Day) months--;
        return Math.Max(0, months);
    }

    private static FeeLine Line(string code, string text, decimal amount) => new() { Code = code, Description = text, Amount = amount };

    private static string Code(BusinessCategory c) => c switch
    {
        BusinessCategory.FoodPremises => "FOOD",
        BusinessCategory.AlcoholOnSale => "ALON",
        BusinessCategory.AlcoholOffSale => "ALOF",
        BusinessCategory.TaxiVehicle => "TAXI",
        BusinessCategory.StreetTrading => "STRT",
        BusinessCategory.Entertainment => "ENT",
        BusinessCategory.ScrapMetalDealer => "SCRP",
        BusinessCategory.HairAndBeauty => "HAIR",
        BusinessCategory.AnimalBoarding => "ANML",
        _ => "OTH"
    };

    public static string Display(BusinessCategory c) => c switch
    {
        BusinessCategory.FoodPremises => "Food premises",
        BusinessCategory.AlcoholOnSale => "Alcohol (on-sales)",
        BusinessCategory.AlcoholOffSale => "Alcohol (off-sales)",
        BusinessCategory.TaxiVehicle => "Taxi vehicle",
        BusinessCategory.StreetTrading => "Street trading",
        BusinessCategory.Entertainment => "Public entertainment",
        BusinessCategory.ScrapMetalDealer => "Scrap metal dealer",
        BusinessCategory.HairAndBeauty => "Hair and beauty",
        BusinessCategory.AnimalBoarding => "Animal boarding",
        _ => c.ToString()
    };
}

public static class Money
{
    public static decimal Round(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);
}
