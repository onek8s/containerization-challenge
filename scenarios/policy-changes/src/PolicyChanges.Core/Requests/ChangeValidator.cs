using System.Globalization;
using System.Text.RegularExpressions;

namespace PolicyChanges.Core.Requests;

/// <summary>
/// Checks a change before it is queued. PolicyDesk runs its own checks when the
/// change is entered; these catch what can be caught up front.
/// </summary>
public sealed partial class ChangeValidator
{
    public const int MaxDaysBack = 30;
    public const int MaxDaysAhead = 90;
    public const int MaxBeneficiaries = 4;

    private readonly TimeProvider _time;

    public ChangeValidator(TimeProvider time) => _time = time;

    [GeneratedRegex(@"^\d{5}$")] private static partial Regex FiPostalCode();
    [GeneratedRegex(@"^\d{3} ?\d{2}$")] private static partial Regex SePostalCode();
    [GeneratedRegex(@"^\d{4}$")] private static partial Regex NoPostalCode();

    public DateOnly Today => DateOnly.FromDateTime(_time.GetLocalNow().DateTime);

    public IReadOnlyList<string> Validate(ChangeDraft draft)
    {
        var errors = new List<string>();

        if (!PolicyNumber.IsValid(draft.PolicyNumber))
            errors.Add("Policy number must be HL- followed by 7 digits with a valid check digit.");

        if (!Enum.IsDefined(draft.ChangeType))
        {
            errors.Add("Unknown change type.");
            return errors;
        }

        ValidateEffectiveDate(draft, errors);

        foreach (var unknown in draft.Fields.Keys.Except(ChangeFields.For(draft.ChangeType), StringComparer.OrdinalIgnoreCase))
            errors.Add($"Field {unknown} is not used for {draft.ChangeType}.");

        switch (draft.ChangeType)
        {
            case ChangeType.AddressChange: ValidateAddress(draft, errors); break;
            case ChangeType.PaymentMethodChange: ValidatePayment(draft, errors); break;
            case ChangeType.BeneficiaryChange: ValidateBeneficiaries(draft, errors); break;
            case ChangeType.Cancellation: ValidateCancellation(draft, errors); break;
        }
        return errors;
    }

    private void ValidateEffectiveDate(ChangeDraft draft, List<string> errors)
    {
        var today = Today;
        if (draft.EffectiveDate == default)
        {
            errors.Add("Effective date is required.");
            return;
        }
        if (draft.EffectiveDate < today.AddDays(-MaxDaysBack))
            errors.Add($"Effective date can be at most {MaxDaysBack} days back.");
        if (draft.EffectiveDate > today.AddDays(MaxDaysAhead))
            errors.Add($"Effective date can be at most {MaxDaysAhead} days ahead.");
        if (draft.ChangeType == ChangeType.Cancellation
            && draft.EffectiveDate < today
            && draft.Field(ChangeFields.Reason) is not (CancellationReasons.Death or CancellationReasons.VehicleSold))
            errors.Add("Backdated cancellation is only allowed for death of the policyholder or a sold vehicle.");
    }

    private static void ValidateAddress(ChangeDraft draft, List<string> errors)
    {
        var street = draft.Field(ChangeFields.Street);
        var city = draft.Field(ChangeFields.City);
        var country = draft.Field(ChangeFields.Country).ToUpperInvariant();
        var postal = draft.Field(ChangeFields.PostalCode);

        if (street.Length is < 3 or > 40) errors.Add("Street must be 3-40 characters.");
        if (city.Length is < 2 or > 30) errors.Add("City must be 2-30 characters.");
        if (!Countries.All.Contains(country)) errors.Add("Country must be one of " + string.Join(", ", Countries.All) + ".");

        var postalOk = country switch
        {
            "FI" => FiPostalCode().IsMatch(postal),
            "SE" => SePostalCode().IsMatch(postal),
            "NO" => NoPostalCode().IsMatch(postal),
            "EE" => FiPostalCode().IsMatch(postal),
            _ => true,
        };
        if (!postalOk) errors.Add($"Postal code {postal} is not valid for {country}.");
    }

    private static void ValidatePayment(ChangeDraft draft, List<string> errors)
    {
        var method = draft.Field(ChangeFields.PaymentMethod);
        if (!PaymentMethods.All.Contains(method))
        {
            errors.Add("Payment method must be DirectDebit or Invoice.");
            return;
        }

        if (method == PaymentMethods.DirectDebit)
        {
            if (!Iban.IsValid(draft.Field(ChangeFields.Iban))) errors.Add("IBAN is not valid.");
            if (draft.Field(ChangeFields.AccountHolder).Length is < 2 or > 40) errors.Add("Account holder must be 2-40 characters.");
        }
        else if (draft.Field(ChangeFields.Iban).Length > 0)
        {
            errors.Add("IBAN is only given for direct debit.");
        }

        var day = draft.Field(ChangeFields.BillingDay);
        if (!int.TryParse(day, NumberStyles.None, CultureInfo.InvariantCulture, out var d) || d is < 1 or > 28)
            errors.Add("Billing day must be 1-28.");
    }

    private void ValidateBeneficiaries(ChangeDraft draft, List<string> errors)
    {
        var list = Beneficiary.ParseList(draft.Field(ChangeFields.Beneficiaries), out var parseErrors);
        errors.AddRange(parseErrors);
        if (list.Count == 0) errors.Add("At least one beneficiary is required.");
        if (list.Count > MaxBeneficiaries) errors.Add($"At most {MaxBeneficiaries} beneficiaries.");
        if (parseErrors.Count == 0 && list.Count > 0 && list.Sum(b => b.Share) != 100)
            errors.Add("Beneficiary shares must total 100.");
        if (list.Any(b => b.BirthDate > Today))
            errors.Add("Date of birth cannot be in the future.");
    }

    private static void ValidateCancellation(ChangeDraft draft, List<string> errors)
    {
        if (!CancellationReasons.All.Contains(draft.Field(ChangeFields.Reason)))
            errors.Add("Reason must be one of " + string.Join(", ", CancellationReasons.All) + ".");
        var refund = draft.Field(ChangeFields.RefundIban);
        if (refund.Length > 0 && !Iban.IsValid(refund)) errors.Add("Refund IBAN is not valid.");
    }
}
