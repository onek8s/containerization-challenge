using ExpenseClaims.Core.Accounting;
using ExpenseClaims.Core.Claims;

namespace ExpenseClaims.Tests.Core;

public class AccountingTests
{
    private readonly AccountingOptions _options = new();

    [Theory]
    [InlineData(ExpenseCategory.Accommodation, "EUR", null, 10)]
    [InlineData(ExpenseCategory.Accommodation, "EUR", "FI", 10)]
    [InlineData(ExpenseCategory.Accommodation, "SEK", "SE", 0)]
    [InlineData(ExpenseCategory.Accommodation, "EUR", "DE", 0)]
    [InlineData(ExpenseCategory.Meals, "EUR", null, 0)]
    [InlineData(ExpenseCategory.Representation, "EUR", null, 0)]
    [InlineData(ExpenseCategory.Training, "EUR", null, 24)]
    [InlineData(ExpenseCategory.Mileage, "EUR", null, 0)]
    public void Deductible_vat_rate(ExpenseCategory category, string currency, string? country, decimal expected) =>
        Assert.Equal(expected, new VatRules(_options).RateFor(category, currency, country));

    [Theory]
    [InlineData(110, 10, 10)]
    [InlineData(124, 24, 24)]
    [InlineData(99.90, 24, 19.34)]
    [InlineData(50, 0, 0)]
    public void Vat_is_taken_out_of_the_gross_amount(decimal gross, decimal rate, decimal vat) =>
        Assert.Equal(vat, VatRules.VatOf(gross, rate));

    [Fact]
    public void Journal_balances_and_splits_employee_and_card_parts()
    {
        var claim = new JournalClaim("EC-2022-000042", "100233", "Aino Laine", "2140", "PR20114", new DateOnly(2022, 10, 14), new[]
        {
            new JournalClaimLine(1, ExpenseCategory.Accommodation, "Hotel", 132m, 12m, PaidByCompany: true),
            new JournalClaimLine(2, ExpenseCategory.PerDiem, "Daily allowance", 48m, 0m, PaidByCompany: false),
            new JournalClaimLine(3, ExpenseCategory.Mileage, "Espoo - airport", 19.32m, 0m, PaidByCompany: false)
        });

        var lines = new JournalBuilder(_options).Build(claim);

        Assert.Equal(0m, lines.Sum(l => l.Amount));
        Assert.Contains(lines, l => l.AccountNo == "7620" && l.Amount == 120m && l.CostCenter == "2140" && l.Project == "PR20114");
        Assert.Contains(lines, l => l.AccountNo == "2930" && l.Amount == 12m && l.CostCenter == null);
        Assert.Contains(lines, l => l.AccountNo == "2870" && l.Amount == -67.32m && l.EmployeeNumber == "100233");
        Assert.Contains(lines, l => l.AccountNo == "2875" && l.Amount == -132m);
        Assert.Equal(lines.Select(l => l.LineNo).Distinct().Count(), lines.Count);
        Assert.All(lines, l => Assert.True(l.Description.Length <= 50));
    }

    [Theory]
    [InlineData("3005", ExpenseCategory.Travel)]
    [InlineData("3664", ExpenseCategory.Accommodation)]
    [InlineData("7011", ExpenseCategory.Accommodation)]
    [InlineData("5814", ExpenseCategory.Meals)]
    [InlineData("4112", ExpenseCategory.Travel)]
    [InlineData("5732", ExpenseCategory.Equipment)]
    [InlineData("5411", ExpenseCategory.Other)]
    [InlineData("", ExpenseCategory.Other)]
    public void Merchant_category_suggestion(string mcc, ExpenseCategory expected) =>
        Assert.Equal(expected, MerchantCategories.Suggest(mcc));

    [Fact]
    public void Claim_number_round_trip()
    {
        var number = ClaimNumber.Format(2022, 42);
        Assert.Equal("EC-2022-000042", number);
        Assert.True(ClaimNumber.TryParse(number, out var year, out var sequence));
        Assert.Equal((2022, 42), (year, sequence));
        Assert.False(ClaimNumber.TryParse("EC-22-42", out _, out _));
    }
}
