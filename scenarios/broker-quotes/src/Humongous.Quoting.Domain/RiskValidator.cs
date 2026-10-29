using FluentValidation;

namespace Humongous.Quoting.Domain;

public sealed class RiskValidator : AbstractValidator<RiskDetails>
{
    public RiskValidator()
    {
        RuleFor(r => r.Insured.Name).NotEmpty().MaximumLength(120).OverridePropertyName("insured.name");
        RuleFor(r => r.Insured.AddressLine1).NotEmpty().MaximumLength(100).OverridePropertyName("insured.addressLine1");
        RuleFor(r => r.Insured.Town).NotEmpty().MaximumLength(60).OverridePropertyName("insured.town");
        RuleFor(r => r.Insured.Postcode)
            .Must(Postcode.IsValid).WithMessage("Enter a valid UK postcode.")
            .OverridePropertyName("insured.postcode");
        RuleFor(r => r.Insured.TradeCode)
            .Must(code => TradeList.Find(code) is not null).WithMessage("Unknown trade code.")
            .OverridePropertyName("insured.tradeCode");
        RuleFor(r => r.Insured.YearsTrading).InclusiveBetween(0, 200).OverridePropertyName("insured.yearsTrading");
        RuleFor(r => r.Insured.Turnover).InclusiveBetween(0m, 25_000_000m).OverridePropertyName("insured.turnover");
        RuleFor(r => r.Insured.ContactEmail).EmailAddress().When(r => !string.IsNullOrEmpty(r.Insured.ContactEmail))
            .OverridePropertyName("insured.contactEmail");

        RuleFor(r => r.Premises.YearBuilt)
            .InclusiveBetween(1500, DateTime.Today.Year)
            .When(r => r.MaterialDamage is not null)
            .OverridePropertyName("premises.yearBuilt");

        RuleFor(r => r)
            .Must(r => r.SelectedSections().Any())
            .WithMessage("Select at least one section of cover.")
            .OverridePropertyName("sections");

        When(r => r.MaterialDamage is not null, () =>
        {
            RuleFor(r => r.MaterialDamage!.Buildings).GreaterThanOrEqualTo(0).OverridePropertyName("materialDamage.buildings");
            RuleFor(r => r.MaterialDamage!.Contents).GreaterThanOrEqualTo(0).OverridePropertyName("materialDamage.contents");
            RuleFor(r => r.MaterialDamage!.Stock).GreaterThanOrEqualTo(0).OverridePropertyName("materialDamage.stock");
            RuleFor(r => r.MaterialDamage!.ComputerEquipment).GreaterThanOrEqualTo(0).OverridePropertyName("materialDamage.computerEquipment");
            RuleFor(r => r.MaterialDamage!.TotalSumInsured).GreaterThan(0)
                .WithMessage("Material damage needs a sum insured.").OverridePropertyName("materialDamage");
            RuleFor(r => r.MaterialDamage!.Excess).Must(e => e is 100m or 250m or 500m or 1000m)
                .WithMessage("Excess must be 100, 250, 500 or 1000.").OverridePropertyName("materialDamage.excess");
        });

        When(r => r.BusinessInterruption is not null, () =>
        {
            RuleFor(r => r.BusinessInterruption!.GrossProfit).GreaterThan(0).OverridePropertyName("businessInterruption.grossProfit");
            RuleFor(r => r.BusinessInterruption!.IndemnityPeriodMonths).Must(m => m is 12 or 18 or 24 or 36)
                .WithMessage("Indemnity period must be 12, 18, 24 or 36 months.")
                .OverridePropertyName("businessInterruption.indemnityPeriodMonths");
            RuleFor(r => r.MaterialDamage).NotNull()
                .WithMessage("Business interruption needs material damage cover.")
                .OverridePropertyName("businessInterruption");
        });

        When(r => r.EmployersLiability is not null, () =>
        {
            RuleFor(r => r.EmployersLiability!.Employees).GreaterThan(0).OverridePropertyName("employersLiability.employees");
            RuleFor(r => r.EmployersLiability!.Wageroll).GreaterThan(0).OverridePropertyName("employersLiability.wageroll");
            RuleFor(r => r.EmployersLiability!.ManualWageroll)
                .LessThanOrEqualTo(r => r.EmployersLiability!.Wageroll)
                .OverridePropertyName("employersLiability.manualWageroll");
        });

        When(r => r.PublicLiability is not null, () =>
        {
            RuleFor(r => r.PublicLiability!.LimitOfIndemnity)
                .Must(l => PublicLiabilityCover.AvailableLimits.Contains(l))
                .WithMessage("Limit of indemnity must be 1m, 2m or 5m.")
                .OverridePropertyName("publicLiability.limitOfIndemnity");
            RuleFor(r => r.Insured.Turnover).GreaterThan(0)
                .WithMessage("Public liability is rated on turnover.").OverridePropertyName("insured.turnover");
        });

        RuleForEach(r => r.Claims).ChildRules(c =>
        {
            c.RuleFor(x => x.Amount).GreaterThanOrEqualTo(0);
            c.RuleFor(x => x.Description).NotEmpty().MaximumLength(200);
            c.RuleFor(x => x.Date).LessThanOrEqualTo(DateOnly.FromDateTime(DateTime.Today));
        }).OverridePropertyName("claims");
    }
}

public sealed class InceptionValidator
{
    public const int MaxDaysForward = 90;

    public static string? Check(DateOnly inception, DateOnly today)
    {
        if (inception < today) return "Inception date cannot be in the past.";
        if (inception > today.AddDays(MaxDaysForward)) return $"Inception date must be within {MaxDaysForward} days.";
        return null;
    }
}
