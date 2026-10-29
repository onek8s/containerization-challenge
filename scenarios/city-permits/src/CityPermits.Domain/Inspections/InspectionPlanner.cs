using CityPermits.Domain.Model;

namespace CityPermits.Domain.Fees
{
    public static class InspectionPlanner
    {
        public static IReadOnlyList<string> StagesFor(PermitCase c) => CityPermits.Domain.Inspections.InspectionStages.For(c);
    }
}

namespace CityPermits.Domain.Inspections
{
    public static class InspectionStages
    {
        public static IReadOnlyList<string> For(PermitCase c) => c.Kind switch
        {
            PermitKind.BuildingPermit => c.Building?.WorkType switch
            {
                WorkType.NewBuilding => new[] { "Foundations", "Drainage", "Frame", "Pre-plaster", "Completion" },
                WorkType.Extension => new[] { "Foundations", "Drainage", "Completion" },
                WorkType.Demolition => new[] { "Site clearance" },
                WorkType.ChangeOfUse => new[] { "Fire safety", "Completion" },
                WorkType.Alteration => new[] { "Completion" },
                _ => Array.Empty<string>()
            },
            PermitKind.BusinessLicence => c.Business?.Category switch
            {
                BusinessCategory.FoodPremises => new[] { "Food hygiene" },
                BusinessCategory.TaxiVehicle => new[] { "Vehicle test" },
                BusinessCategory.AnimalBoarding => new[] { "Animal welfare" },
                BusinessCategory.Entertainment => new[] { "Fire safety", "Licensing visit" },
                BusinessCategory.AlcoholOnSale => new[] { "Licensing visit" },
                _ => Array.Empty<string>()
            },
            _ => Array.Empty<string>()
        };

        public static IReadOnlyList<ChecklistItem> Checklist(string stage) => stage switch
        {
            "Foundations" => new ChecklistItem[]
            {
                new("FDN-1", "Trench depth matches approved drawings", true),
                new("FDN-2", "Bearing strata suitable, no made ground", true),
                new("FDN-3", "Trees within influence distance considered", false),
                new("FDN-4", "Reinforcement as specified (if any)", false),
            },
            "Drainage" => new ChecklistItem[]
            {
                new("DRN-1", "Air test passed", true),
                new("DRN-2", "Falls adequate, access points provided", true),
                new("DRN-3", "Connection to correct sewer (foul/surface)", true),
            },
            "Frame" => new ChecklistItem[]
            {
                new("FRM-1", "Structural members as per calculations", true),
                new("FRM-2", "Lateral restraint straps fitted", true),
            },
            "Pre-plaster" => new ChecklistItem[]
            {
                new("PPL-1", "Insulation continuous, no thermal bridges", true),
                new("PPL-2", "Fire stopping at compartment walls", true),
            },
            "Completion" => new ChecklistItem[]
            {
                new("CMP-1", "Smoke alarms interlinked and working", true),
                new("CMP-2", "Escape windows provided where required", true),
                new("CMP-3", "Electrical certificate received", true),
                new("CMP-4", "Energy performance certificate received", false),
            },
            "Food hygiene" => new ChecklistItem[]
            {
                new("FHY-1", "Hand washing facilities with hot water", true),
                new("FHY-2", "Fridge temperatures below 8 °C", true),
                new("FHY-3", "Food safety management system in place", true),
                new("FHY-4", "Pest control contract", false),
                new("FHY-5", "Allergen information available", true),
            },
            "Fire safety" => new ChecklistItem[]
            {
                new("FIR-1", "Fire risk assessment available", true),
                new("FIR-2", "Escape routes clear and signed", true),
                new("FIR-3", "Emergency lighting tested", true),
            },
            "Vehicle test" => new ChecklistItem[]
            {
                new("VEH-1", "Vehicle under 10 years old", true),
                new("VEH-2", "Taxi meter calibrated and sealed", true),
                new("VEH-3", "CCTV fitted per policy", false),
            },
            _ => new ChecklistItem[] { new("GEN-1", "Premises as described in the application", true) }
        };
    }

    public sealed record ChecklistItem(string Code, string Text, bool Mandatory);

    public sealed record ChecklistAnswer(string Code, bool? Passed, string? Note);

    public static class InspectionOutcome
    {
        public static InspectionStatus Evaluate(string stage, IEnumerable<ChecklistAnswer> answers)
        {
            var map = answers.ToDictionary(a => a.Code);
            var items = InspectionStages.Checklist(stage);
            var mandatoryFailed = items.Any(i => i.Mandatory && map.TryGetValue(i.Code, out var a) && a.Passed == false);
            var mandatoryMissing = items.Any(i => i.Mandatory && (!map.TryGetValue(i.Code, out var a) || a.Passed is null));
            if (mandatoryFailed) return InspectionStatus.Failed;
            if (mandatoryMissing) throw new ArgumentException("All mandatory checklist items must be answered.");
            var optionalFailed = items.Any(i => !i.Mandatory && map.TryGetValue(i.Code, out var a) && a.Passed == false);
            return optionalFailed ? InspectionStatus.PassedWithRemarks : InspectionStatus.Passed;
        }
    }
}
