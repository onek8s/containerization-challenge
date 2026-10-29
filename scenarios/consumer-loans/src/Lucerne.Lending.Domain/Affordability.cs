namespace Lucerne.Lending.Domain;

public sealed record AffordabilityResult(
    decimal HouseholdNetIncome,
    decimal LivingCosts,
    decimal HousingCosts,
    decimal ExistingDebtService,
    decimal NewLoanPaymentStressed,
    decimal Surplus,
    decimal DebtServiceToIncome,
    decimal MaxAffordablePayment);

/// <summary>Household budget check, credit policy CP-CL-04 section 6.</summary>
public sealed class AffordabilityCalculator
{
    /// <summary>Monthly living cost norm per adult and per child (EUR), updated each January by Risk.</summary>
    public decimal AdultNorm { get; init; } = 750m;
    public decimal SecondAdultNorm { get; init; } = 520m;
    public decimal ChildNorm { get; init; } = 330m;
    /// <summary>Interest stress added to the offered rate (percentage points).</summary>
    public decimal StressAddOn { get; init; } = 3m;
    /// <summary>Minimum housing cost used when declared lower (renters).</summary>
    public decimal MinimumRent { get; init; } = 450m;

    public AffordabilityResult Calculate(LoanApplication application, decimal offeredRatePercent, decimal monthlyFee)
    {
        var applicants = application.Applicants;
        var income = applicants.Sum(a => a.NetMonthlyIncome);
        var main = application.Main;
        var adults = Math.Max(main.Adults, applicants.Count);
        var children = applicants.Max(a => a.Children);
        var living = AdultNorm + Math.Max(0, adults - 1) * SecondAdultNorm + children * ChildNorm;
        var housing = applicants.Sum(a => a.HousingCostMonthly);
        if (main.Housing == HousingType.Rent && housing < MinimumRent) housing = MinimumRent;
        var debt = applicants.Sum(a => a.OtherLoanPaymentsMonthly);
        var stressed = Annuity.MonthlyPayment(application.Amount, offeredRatePercent + StressAddOn, application.TermMonths) + monthlyFee;
        var surplus = income - living - housing - debt - stressed;
        var gross = applicants.Sum(a => a.GrossMonthlyIncome);
        var dsti = gross <= 0 ? 9.99m : Math.Round((debt + stressed) / gross, 4);
        var maxPayment = Math.Max(0, income - living - housing - debt);
        return new AffordabilityResult(income, living, housing, debt, stressed, surplus, dsti, maxPayment);
    }
}
