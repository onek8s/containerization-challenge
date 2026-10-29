using CityPermits.Domain;
using CityPermits.Domain.Fees;
using CityPermits.Domain.Inspections;
using CityPermits.Domain.Model;
using CityPermits.Domain.Rules;
using CityPermits.Domain.Workflow;
using Xunit;

namespace CityPermits.Tests;

public class FeeTests
{
    private readonly FeeCalculator _calc = new(new FeeSchedule());
    private static readonly ZoneTariff ZoneA = new() { ZoneCode = "A", ResidentAnnual = 96m, BusinessAnnual = 420m, VisitorBookOf20 = 30m };

    [Theory]
    [InlineData(90_000, 950)]
    [InlineData(100_000, 950)]
    [InlineData(150_000, 950 + 50 * 4.20)]
    [InlineData(250_001, 1580 + 1 * 3.10)]
    [InlineData(1_500_000, 3905 + 500 * 1.75)]
    public void New_build_fee_follows_cost_bands(decimal cost, decimal expected) => Assert.Equal(expected, _calc.CostBased(cost));

    [Fact]
    public void Extension_in_flood_zone_and_conservation_area_adds_surcharges_and_inspections()
    {
        var c = new PermitCase { Kind = PermitKind.BuildingPermit, Building = new BuildingDetails { WorkType = WorkType.Extension, GrossFloorAreaM2 = 35 } };
        var site = new SiteConstraints { FloodZone = "3a", HeritageAreas = { "Ashford Town Centre" } };
        var q = _calc.Calculate(c, site, null);
        Assert.Equal(new[] { "BP-EXT", "BP-HER", "BP-FLD", "BP-INS" }, q.Lines.Select(l => l.Code));
        Assert.Equal(410m + 180m + 145m + 3 * 95m, q.Total);
    }

    [Fact]
    public void Second_resident_permit_with_high_emissions_costs_more()
    {
        var c = new PermitCase { Kind = PermitKind.ParkingPermit, Parking = new ParkingDetails { PermitType = ParkingPermitType.Resident, EmissionBand = VehicleEmissionBand.High, PermitNumberInHousehold = 2, DurationMonths = 6 } };
        Assert.Equal(Money.Round(96m * 1.4m * 1.5m * 6 / 12), _calc.Calculate(c, null, ZoneA).Total);
    }

    [Fact]
    public void Seasonal_street_trading_is_discounted_and_renewal_has_no_admin_fee()
    {
        var c = new PermitCase { Kind = PermitKind.BusinessLicence, RenewalOfCaseId = 9, Business = new BusinessDetails { Category = BusinessCategory.StreetTrading, Seasonal = true } };
        Assert.Equal(384m, _calc.Calculate(c, null, null).Total);
    }

    [Fact]
    public void Large_venue_pays_capacity_surcharge()
    {
        var c = new PermitCase { Kind = PermitKind.BusinessLicence, Business = new BusinessDetails { Category = BusinessCategory.Entertainment, Capacity = 450 } };
        var q = _calc.Calculate(c, null, null);
        Assert.Contains(q.Lines, l => l.Code == "BL-CAP" && l.Amount == 4 * 85m);
    }

    [Fact]
    public void Surrender_refunds_whole_unused_months_less_admin()
    {
        var c = new PermitCase { ValidFrom = new DateOnly(2024, 1, 1), ValidTo = new DateOnly(2024, 12, 31), Fees = { new FeeLine { Amount = 120m } } };
        Assert.Equal(44.55m, _calc.SurrenderRefund(c, new DateOnly(2024, 7, 15)));
    }
}

public class WorkflowTests
{
    private static PermitCase NewCase() => new()
    {
        Kind = PermitKind.BusinessLicence, ApplicantId = 1, Uprn = "1", Business = new BusinessDetails(), Fees = { new FeeLine { Amount = 100m } }
    };

    [Fact]
    public void Happy_path_to_issue()
    {
        var c = NewCase();
        var now = DateTime.UtcNow;
        CaseWorkflow.Apply(c, CaseWorkflow.Submit, "a", new[] { Roles.Applicant }, null, now);
        c.Payments.Add(new Payment { Amount = 100m, Status = PaymentStatus.Settled });
        CaseWorkflow.Apply(c, CaseWorkflow.PaymentReceived, "s", new[] { Roles.System }, null, now);
        CaseWorkflow.Apply(c, CaseWorkflow.StartIntake, "o", new[] { Roles.CaseOfficer }, null, now);
        CaseWorkflow.Apply(c, CaseWorkflow.Validate, "o", new[] { Roles.CaseOfficer }, null, now);
        CaseWorkflow.Apply(c, CaseWorkflow.Recommend, "o", new[] { Roles.CaseOfficer }, "ok", now);
        CaseWorkflow.Apply(c, CaseWorkflow.Approve, "s", new[] { Roles.Supervisor }, null, now);
        CaseWorkflow.Apply(c, CaseWorkflow.Issue, "o", new[] { Roles.CaseOfficer }, null, now);
        Assert.Equal(CaseStatus.Issued, c.Status);
        Assert.Equal(DecisionOutcome.Approved, c.Outcome);
        Assert.Equal(7, c.Events.Count);
    }

    [Fact]
    public void Officer_cannot_approve_and_unpaid_case_cannot_be_approved()
    {
        var c = NewCase();
        c.Status = CaseStatus.DecisionPending;
        Assert.Throws<WorkflowException>(() => CaseWorkflow.Apply(c, CaseWorkflow.Approve, "o", new[] { Roles.CaseOfficer }, null, DateTime.UtcNow));
        var ex = Assert.Throws<WorkflowException>(() => CaseWorkflow.Apply(c, CaseWorkflow.Approve, "s", new[] { Roles.Supervisor }, null, DateTime.UtcNow));
        Assert.Contains("outstanding", ex.Message);
    }

    [Fact]
    public void Refusal_requires_a_note()
    {
        var c = NewCase();
        c.Status = CaseStatus.DecisionPending;
        Assert.Throws<WorkflowException>(() => CaseWorkflow.Apply(c, CaseWorkflow.Refuse, "s", new[] { Roles.Supervisor }, " ", DateTime.UtcNow));
    }

    [Fact]
    public void Building_case_needs_site_checks_before_validation()
    {
        var c = new PermitCase { Kind = PermitKind.BuildingPermit, Status = CaseStatus.IntakeCheck, Building = new BuildingDetails() };
        Assert.Throws<WorkflowException>(() => CaseWorkflow.Apply(c, CaseWorkflow.Validate, "o", new[] { Roles.CaseOfficer }, null, DateTime.UtcNow));
    }

    [Fact]
    public void Working_days_skip_weekends_and_bank_holidays()
    {
        var holidays = new HashSet<DateOnly> { new(2024, 12, 25), new(2024, 12, 26) };
        Assert.Equal(new DateOnly(2024, 12, 30), TargetDates.AddWorkingDays(new DateOnly(2024, 12, 23), 3, holidays));
    }

    [Theory]
    [InlineData(2024, 417)]
    [InlineData(2025, 1)]
    public void References_carry_a_valid_check_letter(int year, int seq)
    {
        var r = ReferenceNumbers.Format(PermitKind.BuildingPermit, year, seq);
        Assert.True(ReferenceNumbers.IsValid(r));
        Assert.False(ReferenceNumbers.IsValid(r[..^1] + (r[^1] == 'A' ? 'B' : 'A')));
    }
}

public class RulesTests
{
    [Fact]
    public void Over_height_easement_and_coverage_are_blocking()
    {
        var c = new PermitCase { Kind = PermitKind.BuildingPermit, Building = new BuildingDetails { WorkType = WorkType.Extension, HeightM = 11, FootprintM2 = 400, UseClass = "C3" } };
        var s = new SiteConstraints { ZoningCode = "R1", ZoningName = "Residential", MaxHeightM = 9, MaxSiteCoverage = 0.4, ParcelAreaM2 = 1100, ExistingCoverageM2 = 100, PermittedUseClasses = { "C3" }, Easements = { "EAS-1" }, EasementOverlapM2 = 4 };
        var codes = PlanningRules.Evaluate(c, s).Where(f => f.Severity == FindingSeverity.Blocking).Select(f => f.Code).ToList();
        Assert.Equal(new[] { "ZON-HGT", "ZON-COV", "EAS-OVL" }, codes);
    }

    [Fact]
    public void Alcohol_near_school_needs_police_consultation()
    {
        var c = new PermitCase { Kind = PermitKind.BusinessLicence, Business = new BusinessDetails { Category = BusinessCategory.AlcoholOffSale } };
        var f = PlanningRules.Evaluate(c, new SiteConstraints { DistanceToSchoolM = 160 });
        Assert.Contains(f, x => x.Code == "LIC-SCH" && x.Consultee == "Police Licensing");
    }

    [Fact]
    public void Parking_outside_zone_is_blocked()
    {
        var c = new PermitCase { Kind = PermitKind.ParkingPermit, Parking = new ParkingDetails { PermitType = ParkingPermitType.Resident } };
        Assert.Contains(PlanningRules.Evaluate(c, new SiteConstraints()), x => x.Code == "PRK-ZONE");
    }

    [Fact]
    public void Renewal_reminders_follow_the_schedule()
    {
        var c = new PermitCase { Kind = PermitKind.BusinessLicence, Status = CaseStatus.Issued, ValidTo = new DateOnly(2025, 3, 31) };
        Assert.Null(RenewalPolicy.Due(c, new DateOnly(2025, 1, 15)));
        Assert.Equal(1, RenewalPolicy.Due(c, new DateOnly(2025, 1, 31))!.ReminderNumber);
        c.RenewalReminderCount = 1;
        Assert.Null(RenewalPolicy.Due(c, new DateOnly(2025, 2, 15)));
        Assert.Equal(2, RenewalPolicy.Due(c, new DateOnly(2025, 3, 2))!.ReminderNumber);
    }

    [Fact]
    public void Renewal_continues_from_previous_expiry()
    {
        var prev = new PermitCase { ValidTo = new DateOnly(2025, 3, 31) };
        var c = new PermitCase { Kind = PermitKind.ParkingPermit, Parking = new ParkingDetails { DurationMonths = 12 } };
        Assert.Equal((new DateOnly(2025, 4, 1), new DateOnly(2026, 3, 31)), RenewalPolicy.Validity(c, new DateOnly(2025, 3, 20), prev));
    }

    [Fact]
    public void Food_hygiene_failure_on_mandatory_item_fails_visit()
    {
        var answers = InspectionStages.Checklist("Food hygiene").Select(i => new ChecklistAnswer(i.Code, i.Code != "FHY-2", null));
        Assert.Equal(InspectionStatus.Failed, InspectionOutcome.Evaluate("Food hygiene", answers));
    }

    [Theory]
    [InlineData("AB12 CDE", true)]
    [InlineData("KN18OPX", true)]
    [InlineData("12345", false)]
    public void Registration_format(string reg, bool ok) => Assert.Equal(ok, PermitCaseValidator.Registration.IsMatch(reg));
}
