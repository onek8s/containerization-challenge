using System.Text.RegularExpressions;
using CityPermits.Domain.Model;
using FluentValidation;

namespace CityPermits.Domain.Rules;

public sealed class PermitCaseValidator : AbstractValidator<PermitCase>
{
    public static readonly Regex Registration = new(@"^[A-Z]{2}[0-9]{2}\s?[A-Z]{3}$|^[A-Z][0-9]{1,3}\s?[A-Z]{3}$|^[A-Z]{3}\s?[0-9]{1,3}[A-Z]$", RegexOptions.Compiled);

    public PermitCaseValidator()
    {
        RuleFor(c => c.Title).NotEmpty().MaximumLength(200);
        RuleFor(c => c.Description).MaximumLength(4000);

        When(c => c.Kind == PermitKind.BuildingPermit, () =>
        {
            RuleFor(c => c.Building).NotNull();
            RuleFor(c => c.Building!.EstimatedCost).GreaterThan(0).When(c => c.Building?.WorkType == WorkType.NewBuilding)
                .WithMessage("Estimated cost is required for new buildings.");
            RuleFor(c => c.Building!.GrossFloorAreaM2).GreaterThan(0).When(c => c.Building?.WorkType is WorkType.Extension or WorkType.NewBuilding);
            RuleFor(c => c.Building!.HeightM).InclusiveBetween(0, 150).When(c => c.Building is not null);
            RuleFor(c => c.Building!.Storeys).InclusiveBetween(0, 40).When(c => c.Building is not null);
            RuleFor(c => c.Building!.UseClass).NotEmpty().When(c => c.Building?.WorkType == WorkType.ChangeOfUse);
        });

        When(c => c.Kind == PermitKind.BusinessLicence, () =>
        {
            RuleFor(c => c.Business).NotNull();
            RuleFor(c => c.Business!.TradingName).NotEmpty().MaximumLength(120).When(c => c.Business is not null);
            RuleFor(c => c.Business!.ResponsiblePerson).NotEmpty()
                .When(c => c.Business?.Category is BusinessCategory.AlcoholOnSale or BusinessCategory.AlcoholOffSale)
                .WithMessage("A designated premises supervisor is required for alcohol licences.");
            RuleFor(c => c.Business!.Capacity).NotNull().GreaterThan(0)
                .When(c => c.Business?.Category is BusinessCategory.Entertainment or BusinessCategory.AlcoholOnSale);
        });

        When(c => c.Kind == PermitKind.ParkingPermit, () =>
        {
            RuleFor(c => c.Parking).NotNull();
            RuleFor(c => c.Parking!.Registration).NotEmpty()
                .Must(r => r is null || Registration.IsMatch(r.ToUpperInvariant()))
                .When(c => c.Parking?.PermitType is ParkingPermitType.Resident or ParkingPermitType.Business)
                .WithMessage("Enter a valid vehicle registration.");
            RuleFor(c => c.Parking!.DurationMonths).Must(m => m is 3 or 6 or 12).When(c => c.Parking is not null);
            RuleFor(c => c.Uprn).NotEmpty().When(c => c.Parking?.PermitType != ParkingPermitType.Carer)
                .WithMessage("Select the address the permit is for.");
        });
    }
}
