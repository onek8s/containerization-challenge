using System.Globalization;
using ExpenseClaims.Core.Claims;

namespace ExpenseClaims.Core.Policy;

public sealed class ExpensePolicy
{
    private readonly ExpensePolicyOptions _options;

    public ExpensePolicy(ExpensePolicyOptions options) => _options = options;

    public ExpensePolicyOptions Options => _options;

    /// <summary>Amount that is reimbursed for a line before currency conversion.</summary>
    public decimal CalculateAmount(ClaimLineInput line) => line.Category switch
    {
        ExpenseCategory.Mileage => Math.Round((line.Kilometres ?? 0m) * _options.MileageRatePerKm, 2, MidpointRounding.AwayFromZero),
        ExpenseCategory.PerDiem => PerDiemRate(line.PerDiem, line.Country),
        _ => line.Amount
    };

    public decimal PerDiemRate(PerDiemType type, string? country) => type switch
    {
        PerDiemType.Full => _options.PerDiemFull,
        PerDiemType.Partial => _options.PerDiemPartial,
        PerDiemType.Abroad => country is not null && _options.PerDiemAbroad.TryGetValue(country, out var rate)
            ? rate
            : _options.PerDiemAbroadDefault,
        _ => 0m
    };

    /// <summary>Checks the lines of a claim. <paramref name="baseAmounts"/> holds each line's amount in the base currency.</summary>
    public IReadOnlyList<PolicyFinding> Check(
        IReadOnlyList<ClaimLineInput> lines,
        IReadOnlyDictionary<int, decimal> baseAmounts,
        DateOnly today)
    {
        var findings = new List<PolicyFinding>();
        if (lines.Count == 0)
        {
            findings.Add(new PolicyFinding(0, FindingSeverity.Error, "NO_LINES", "The claim has no expense lines."));
            return findings;
        }

        foreach (var line in lines)
        {
            var amount = baseAmounts.TryGetValue(line.LineNumber, out var a) ? a : line.Amount;
            CheckLine(line, amount, today, findings);
        }

        foreach (var day in lines.Where(l => l.Category == ExpenseCategory.PerDiem).GroupBy(l => l.ExpenseDate).Where(g => g.Count() > 1))
        {
            foreach (var line in day.Skip(1))
                findings.Add(new PolicyFinding(line.LineNumber, FindingSeverity.Error, "PERDIEM_DUPLICATE",
                    $"Only one daily allowance can be claimed for {Day(day.Key)}."));
        }

        var perDiemDays = lines.Where(l => l.Category == ExpenseCategory.PerDiem).Select(l => l.ExpenseDate).ToHashSet();
        foreach (var meal in lines.Where(l => l.Category == ExpenseCategory.Meals && perDiemDays.Contains(l.ExpenseDate)))
        {
            findings.Add(new PolicyFinding(meal.LineNumber, FindingSeverity.Warning, "MEAL_WITH_PERDIEM",
                $"A meal is claimed on {Day(meal.ExpenseDate)} together with a daily allowance."));
        }

        return findings;
    }

    private void CheckLine(ClaimLineInput line, decimal amount, DateOnly today, List<PolicyFinding> findings)
    {
        void Add(FindingSeverity severity, string code, string message) =>
            findings.Add(new PolicyFinding(line.LineNumber, severity, code, message));

        if (line.ExpenseDate > today)
            Add(FindingSeverity.Error, "DATE_FUTURE", "The expense date is in the future.");
        else if (line.ExpenseDate < today.AddDays(-_options.MaxAgeDays))
            Add(FindingSeverity.Error, "DATE_TOO_OLD", $"Expenses older than {_options.MaxAgeDays} days cannot be claimed.");

        if (string.IsNullOrWhiteSpace(line.Description))
            Add(FindingSeverity.Error, "DESCRIPTION_MISSING", "A description is required.");

        switch (line.Category)
        {
            case ExpenseCategory.Mileage:
                if (line.Kilometres is null or <= 0)
                    Add(FindingSeverity.Error, "KM_MISSING", "Kilometres are required for a mileage line.");
                else if (line.Kilometres > _options.MaxKilometresPerLine)
                    Add(FindingSeverity.Error, "KM_TOO_HIGH", $"A mileage line can be at most {_options.MaxKilometresPerLine:0} km.");
                return;

            case ExpenseCategory.PerDiem:
                if (line.PerDiem == PerDiemType.None)
                    Add(FindingSeverity.Error, "PERDIEM_TYPE", "Select full, partial or abroad daily allowance.");
                if (line.PerDiem == PerDiemType.Abroad && string.IsNullOrWhiteSpace(line.Country))
                    Add(FindingSeverity.Error, "PERDIEM_COUNTRY", "Country is required for a daily allowance abroad.");
                return;
        }

        if (line.Amount <= 0)
        {
            Add(FindingSeverity.Error, "AMOUNT", "The amount must be greater than zero.");
            return;
        }

        if (amount >= _options.ReceiptRequiredFrom && !line.HasReceipt)
            Add(FindingSeverity.Error, "RECEIPT_MISSING", $"A receipt is required from {Money(_options.ReceiptRequiredFrom)}.");

        switch (line.Category)
        {
            case ExpenseCategory.Accommodation when amount > _options.AccommodationPerNight:
                Add(FindingSeverity.Warning, "ACCOMMODATION_LIMIT", $"Above the accommodation guideline of {Money(_options.AccommodationPerNight)} per night.");
                break;
            case ExpenseCategory.Meals:
                CheckPerPerson(_options.MealsPerPersonLimit, "MEALS_LIMIT");
                break;
            case ExpenseCategory.Representation:
                if (line.Attendees <= 0)
                    Add(FindingSeverity.Error, "ATTENDEES_MISSING", "Number of attendees is required for representation.");
                else
                    CheckPerPerson(_options.RepresentationPerPersonLimit, "REPRESENTATION_LIMIT");
                break;
            case ExpenseCategory.Equipment when amount > _options.EquipmentLimit:
                Add(FindingSeverity.Error, "EQUIPMENT_LIMIT", $"Equipment above {Money(_options.EquipmentLimit)} must be ordered through procurement.");
                break;
        }

        void CheckPerPerson(decimal limit, string code)
        {
            var people = Math.Max(1, line.Attendees);
            if (amount / people > limit)
                Add(FindingSeverity.Warning, code, $"Above the guideline of {Money(limit)} per person.");
        }
    }

    private string Money(decimal value) => string.Create(CultureInfo.InvariantCulture, $"{value:0.00} {_options.BaseCurrency}");

    private static string Day(DateOnly date) => date.ToString("d.M.yyyy", CultureInfo.InvariantCulture);
}
