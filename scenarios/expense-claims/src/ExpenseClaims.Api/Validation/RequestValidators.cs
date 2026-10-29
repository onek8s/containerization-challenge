using ExpenseClaims.Api.Models;
using ExpenseClaims.Core.Claims;
using FluentValidation;

namespace ExpenseClaims.Api.Validation;

public sealed class CreateClaimRequestValidator : AbstractValidator<CreateClaimRequest>
{
    public CreateClaimRequestValidator()
    {
        RuleFor(x => x.Purpose).NotEmpty().MaximumLength(500);
        RuleFor(x => x.CostCenter).Matches("^[0-9]{4,6}$").When(x => !string.IsNullOrEmpty(x.CostCenter))
            .WithMessage("Cost center is a 4-6 digit number.");
        RuleFor(x => x.ProjectCode).Matches("^[A-Z]{2}[0-9]{4,8}$").When(x => !string.IsNullOrEmpty(x.ProjectCode))
            .WithMessage("Project code is two letters followed by 4-8 digits, for example PR20114.");
    }
}

public sealed class UpdateClaimRequestValidator : AbstractValidator<UpdateClaimRequest>
{
    public UpdateClaimRequestValidator()
    {
        RuleFor(x => x.Purpose).NotEmpty().MaximumLength(500);
        RuleFor(x => x.CostCenter).Matches("^[0-9]{4,6}$").When(x => !string.IsNullOrEmpty(x.CostCenter));
        RuleFor(x => x.ProjectCode).Matches("^[A-Z]{2}[0-9]{4,8}$").When(x => !string.IsNullOrEmpty(x.ProjectCode));
    }
}

public sealed class ClaimLineRequestValidator : AbstractValidator<ClaimLineRequest>
{
    public ClaimLineRequestValidator()
    {
        RuleFor(x => x.Category).IsInEnum();
        RuleFor(x => x.PerDiem).IsInEnum();
        RuleFor(x => x.Description).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Currency).Matches("^[A-Z]{3}$").When(x => x.Currency is not null);
        RuleFor(x => x.Country).Matches("^[A-Z]{2}$").When(x => !string.IsNullOrEmpty(x.Country));
        RuleFor(x => x.Amount).GreaterThanOrEqualTo(0).LessThan(100000);
        RuleFor(x => x.Kilometres).GreaterThan(0).When(x => x.Category == ExpenseCategory.Mileage);
        RuleFor(x => x.Route).NotEmpty().MaximumLength(200).When(x => x.Category == ExpenseCategory.Mileage);
        RuleFor(x => x.Attendees).InclusiveBetween(0, 200);
    }
}

public sealed class DecisionRequestValidator : AbstractValidator<DecisionRequest>
{
    public DecisionRequestValidator()
    {
        RuleFor(x => x.Comment).MaximumLength(1000);
    }
}
