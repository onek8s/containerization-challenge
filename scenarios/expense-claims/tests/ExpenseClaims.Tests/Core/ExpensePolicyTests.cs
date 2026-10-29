using ExpenseClaims.Core.Claims;
using ExpenseClaims.Core.Policy;

namespace ExpenseClaims.Tests.Core;

public class ExpensePolicyTests
{
    private static readonly DateOnly Today = new(2022, 10, 14);
    private readonly ExpensePolicy _policy = new(new ExpensePolicyOptions
    {
        PerDiemAbroad = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase) { ["SE"] = 64m }
    });

    private static ClaimLineInput Line(int n, ExpenseCategory category, decimal amount, bool receipt = true, DateOnly? date = null,
        decimal? km = null, PerDiemType perDiem = PerDiemType.None, string? country = null, int attendees = 0) =>
        new(n, date ?? Today.AddDays(-2), category, amount, "EUR", km, perDiem, country, attendees, receipt, "test");

    private IReadOnlyList<PolicyFinding> Check(params ClaimLineInput[] lines) =>
        _policy.Check(lines, lines.ToDictionary(l => l.LineNumber, l => _policy.CalculateAmount(l)), Today);

    [Fact]
    public void Empty_claim_is_an_error() =>
        Assert.Contains(Check(), f => f.Code == "NO_LINES" && f.Severity == FindingSeverity.Error);

    [Fact]
    public void Receipt_required_from_threshold()
    {
        Assert.DoesNotContain(Check(Line(1, ExpenseCategory.Travel, 24.99m, receipt: false)), f => f.Code == "RECEIPT_MISSING");
        Assert.Contains(Check(Line(1, ExpenseCategory.Travel, 25m, receipt: false)), f => f.Code == "RECEIPT_MISSING");
    }

    [Fact]
    public void Mileage_is_calculated_from_kilometres()
    {
        var line = Line(1, ExpenseCategory.Mileage, 0, receipt: false, km: 42m);
        Assert.Equal(19.32m, _policy.CalculateAmount(line));
        Assert.Empty(Check(line));
    }

    [Fact]
    public void Mileage_without_kilometres_is_an_error() =>
        Assert.Contains(Check(Line(1, ExpenseCategory.Mileage, 0, receipt: false)), f => f.Code == "KM_MISSING");

    [Theory]
    [InlineData(PerDiemType.Full, null, 48)]
    [InlineData(PerDiemType.Partial, null, 22)]
    [InlineData(PerDiemType.Abroad, "SE", 64)]
    [InlineData(PerDiemType.Abroad, "JP", 60)]
    public void Daily_allowance_rates(PerDiemType type, string? country, decimal expected) =>
        Assert.Equal(expected, _policy.PerDiemRate(type, country));

    [Fact]
    public void Only_one_daily_allowance_per_day()
    {
        var findings = Check(
            Line(1, ExpenseCategory.PerDiem, 0, perDiem: PerDiemType.Full),
            Line(2, ExpenseCategory.PerDiem, 0, perDiem: PerDiemType.Partial));
        Assert.Contains(findings, f => f.Code == "PERDIEM_DUPLICATE" && f.LineNumber == 2);
    }

    [Fact]
    public void Meal_on_daily_allowance_day_is_a_warning()
    {
        var findings = Check(
            Line(1, ExpenseCategory.PerDiem, 0, perDiem: PerDiemType.Full),
            Line(2, ExpenseCategory.Meals, 18m, receipt: false));
        Assert.Contains(findings, f => f.Code == "MEAL_WITH_PERDIEM" && f.Severity == FindingSeverity.Warning);
    }

    [Fact]
    public void Old_and_future_dates_are_rejected()
    {
        Assert.Contains(Check(Line(1, ExpenseCategory.Travel, 10m, date: Today.AddDays(-91))), f => f.Code == "DATE_TOO_OLD");
        Assert.Contains(Check(Line(1, ExpenseCategory.Travel, 10m, date: Today.AddDays(1))), f => f.Code == "DATE_FUTURE");
    }

    [Fact]
    public void Representation_needs_attendees_and_is_checked_per_person()
    {
        Assert.Contains(Check(Line(1, ExpenseCategory.Representation, 200m)), f => f.Code == "ATTENDEES_MISSING");
        Assert.DoesNotContain(Check(Line(1, ExpenseCategory.Representation, 340m, attendees: 4)), f => f.Code == "REPRESENTATION_LIMIT");
        Assert.Contains(Check(Line(1, ExpenseCategory.Representation, 380m, attendees: 4)), f => f.Code == "REPRESENTATION_LIMIT");
    }

    [Fact]
    public void Equipment_above_limit_goes_through_procurement() =>
        Assert.Contains(Check(Line(1, ExpenseCategory.Equipment, 349m)), f => f.Code == "EQUIPMENT_LIMIT" && f.Severity == FindingSeverity.Error);
}
