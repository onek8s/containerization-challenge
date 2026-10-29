using CityPermits.Domain.Model;

namespace CityPermits.Domain.Rules;

public enum FindingSeverity { Info, Consult, Condition, Blocking }

public sealed record RuleFinding(string Code, FindingSeverity Severity, string Message, string? Consultee = null, string? ConditionText = null);

/// <summary>
/// Local Plan 2019 development management policies that can be checked mechanically.
/// The officer still decides; findings are advisory except "Blocking", which must be resolved
/// (amended plans or a refusal) before a recommendation can be made.
/// </summary>
public static class PlanningRules
{
    public static IReadOnlyList<RuleFinding> Evaluate(PermitCase c, SiteConstraints s)
    {
        var f = new List<RuleFinding>();
        if (c.Kind == PermitKind.BuildingPermit && c.Building is { } b) Building(b, s, f);
        if (c.Kind == PermitKind.BusinessLicence && c.Business is { } l) Licence(l, s, f);
        if (c.Kind == PermitKind.ParkingPermit && c.Parking is { } p) Parking(p, s, f);
        return f;
    }

    private static void Building(BuildingDetails b, SiteConstraints s, List<RuleFinding> f)
    {
        if (s.ZoningCode is null)
            f.Add(new("ZON-NONE", FindingSeverity.Consult, "Site is not covered by a zoning district; refer to Planning Policy.", "Planning Policy"));

        if (s.MaxHeightM is { } maxH && b.HeightM > maxH)
            f.Add(new("ZON-HGT", b.HeightM > maxH * 1.1 ? FindingSeverity.Blocking : FindingSeverity.Condition,
                $"Proposed height {b.HeightM:0.0} m exceeds the {maxH:0.0} m limit in {s.ZoningName}.",
                ConditionText: "Ridge height shall not exceed the height shown on the approved drawings."));

        if (s.MaxSiteCoverage is { } maxCov && s.ParcelAreaM2 > 0 && b.FootprintM2 > 0)
        {
            var coverage = (s.ExistingCoverageM2 + (b.WorkType == WorkType.Demolition ? -b.FootprintM2 : b.FootprintM2)) / s.ParcelAreaM2;
            if (coverage > maxCov)
                f.Add(new("ZON-COV", FindingSeverity.Blocking,
                    $"Site coverage would be {coverage:P0}, above the {maxCov:P0} maximum."));
            else if (coverage > maxCov * 0.9)
                f.Add(new("ZON-COV", FindingSeverity.Info, $"Site coverage {coverage:P0} is close to the {maxCov:P0} maximum."));
        }

        if (!string.IsNullOrEmpty(b.UseClass) && s.PermittedUseClasses.Count > 0 && !s.PermittedUseClasses.Contains(b.UseClass))
            f.Add(new("ZON-USE", FindingSeverity.Blocking, $"Use class {b.UseClass} is not permitted in {s.ZoningName}."));

        foreach (var h in s.HeritageAreas)
            f.Add(new("HER-CA", FindingSeverity.Consult, $"Site is in the {h} conservation area.", "Conservation Officer",
                "Materials for external surfaces shall match the existing building."));
        if (b.ListedBuilding)
            f.Add(new("HER-LB", FindingSeverity.Consult, "Works affect a listed building.", "Historic Environment Service"));

        if (s.FloodZone is "3" or "3a" or "3b")
            f.Add(new("FLD-3", b.WorkType is WorkType.NewBuilding or WorkType.ChangeOfUse ? FindingSeverity.Consult : FindingSeverity.Condition,
                $"Site is in flood zone {s.FloodZone}.", "Environment Agency",
                "Finished floor levels shall be set no lower than 300 mm above the 1 in 100 year flood level."));
        else if (s.FloodZone == "2")
            f.Add(new("FLD-2", FindingSeverity.Info, "Site is in flood zone 2; a flood risk statement is required."));

        if (s.EasementOverlapM2 > 0.5)
            f.Add(new("EAS-OVL", FindingSeverity.Blocking,
                $"Footprint overlaps {s.EasementOverlapM2:0.0} m² of easement ({string.Join(", ", s.Easements)}).", "Ashford Water"));
        else if (s.Easements.Count > 0)
            f.Add(new("EAS-NEAR", FindingSeverity.Consult, $"Easement within 3 m: {string.Join(", ", s.Easements)}.", "Ashford Water"));

        foreach (var tpo in s.TreePreservationOrders)
            f.Add(new("TPO", FindingSeverity.Condition, $"Tree preservation order {tpo} within the site.", "Tree Officer",
                "No works shall start until tree protection fencing has been erected in accordance with BS 5837."));
    }

    private static void Licence(BusinessDetails l, SiteConstraints s, List<RuleFinding> f)
    {
        if (!string.IsNullOrEmpty(l.UseClass) && s.PermittedUseClasses.Count > 0 && !s.PermittedUseClasses.Contains(l.UseClass))
            f.Add(new("LIC-USE", FindingSeverity.Blocking, $"Premises use class {l.UseClass} is not permitted in {s.ZoningName}; planning permission is needed first."));

        if (l.Category is BusinessCategory.AlcoholOffSale or BusinessCategory.AlcoholOnSale)
        {
            if (s.DistanceToSchoolM is < 400)
                f.Add(new("LIC-SCH", FindingSeverity.Consult, $"School within {s.DistanceToSchoolM:0} m (cumulative impact policy CIP-2).", "Police Licensing"));
            if (s.DistanceToNearestLicensedPremisesM is < 50)
                f.Add(new("LIC-CUM", FindingSeverity.Consult, "Another licensed premises within 50 m (cumulative impact area).", "Police Licensing"));
        }
        if (l.Category == BusinessCategory.FoodPremises)
            f.Add(new("LIC-FHI", FindingSeverity.Info, "Food hygiene inspection required before the licence is issued."));
        if (l.Category == BusinessCategory.Entertainment && l.Capacity > 500)
            f.Add(new("LIC-SAG", FindingSeverity.Consult, "Capacity over 500: Safety Advisory Group review.", "Safety Advisory Group"));
    }

    private static void Parking(ParkingDetails p, SiteConstraints s, List<RuleFinding> f)
    {
        if (s.ParkingZone is null && p.PermitType != ParkingPermitType.Carer)
            f.Add(new("PRK-ZONE", FindingSeverity.Blocking, "Address is not in a controlled parking zone."));
        else if (p.ZoneCode is not null && s.ParkingZone is not null && !string.Equals(p.ZoneCode, s.ParkingZone, StringComparison.OrdinalIgnoreCase))
            f.Add(new("PRK-MISMATCH", FindingSeverity.Blocking, $"Address is in zone {s.ParkingZone}, not {p.ZoneCode}."));
    }
}
