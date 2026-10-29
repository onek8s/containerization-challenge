using Lucerne.Lending.Domain;

namespace Lucerne.Lending.Workflow;

public static class ApplicationValidator
{
    public static IEnumerable<string> Validate(LoanApplication app, LoanProduct? product)
    {
        if (app.DueDay is < 1 or > 28) yield return "Due day must be between 1 and 28.";
        if (!app.ConsentBureau) yield return "Consent to check credit information is required.";
        if (app.DisbursementIban != null && !Iban.IsValid(app.DisbursementIban)) yield return "Disbursement account IBAN is not valid.";
        if (product is { RequiresCollateral: true } && string.IsNullOrWhiteSpace(app.CollateralDescription))
            yield return "Please describe the vehicle (make, model, registration number).";
        var codes = new HashSet<string>();
        foreach (var a in app.Applicants)
        {
            var who = a.IsMain ? "Applicant" : "Co-applicant";
            if (!PersonalIdentityCode.IsValid(a.PersonalIdentityCode)) yield return $"{who}: personal identity code is not valid.";
            else if (!codes.Add(a.PersonalIdentityCode.ToUpperInvariant())) yield return "Co-applicant must be a different person.";
            if (string.IsNullOrWhiteSpace(a.FirstName) || string.IsNullOrWhiteSpace(a.LastName)) yield return $"{who}: name is required.";
            if (!a.Email.Contains('@')) yield return $"{who}: e-mail address is not valid.";
            if (a.NetMonthlyIncome < 0 || a.GrossMonthlyIncome < a.NetMonthlyIncome) yield return $"{who}: gross income must be at least the net income.";
            if (a.Adults < 1 || a.Children < 0) yield return $"{who}: household size is not valid.";
            if (a.PostalCode.Length != 5 || !a.PostalCode.All(char.IsDigit)) yield return $"{who}: postal code must be 5 digits.";
        }
    }
}
