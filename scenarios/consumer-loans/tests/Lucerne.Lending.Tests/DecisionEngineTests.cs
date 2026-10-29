using Lucerne.Lending.Decisioning;
using Lucerne.Lending.Domain;
using Xunit;

namespace Lucerne.Lending.Tests;

public class DecisionEngineTests
{
    private readonly DecisionEngine _engine = new();

    private static CreditFacts Good() => new()
    {
        ProductCode = "PL", Amount = 12000, TermMonths = 60, Age = 41, AgeAtMaturity = 46, Resident = true,
        Employment = "Permanent", MonthsEmployed = 96, Housing = "Owner", NetIncome = 3200, GrossIncome = 4400,
        BureauPersonFound = true, BureauScore = 86, ExistingCustomer = true, CustomerMonths = 140, KycStatus = "C",
        InternalRating = "A", Dsti = 0.18m, Surplus = 1400
    };

    [Fact]
    public async Task Good_applicant_is_approved_with_a_grade()
    {
        var result = await _engine.DecideAsync(Good());
        Assert.Equal(DecisionOutcome.Approve, result.Outcome);
        Assert.Contains(result.Grade, new[] { RiskGrade.A, RiskGrade.B });
        Assert.StartsWith(DecisionEngine.PolicyVersion, result.PolicyVersion);
        Assert.Empty(result.Reasons);
        Assert.NotEmpty(result.ScoreLines);
    }

    [Fact]
    public async Task Payment_default_is_a_knock_out()
    {
        var facts = Good();
        facts.PaymentDefault = true;
        facts.PaymentDefaultCount = 2;
        var result = await _engine.DecideAsync(facts);
        Assert.Equal(DecisionOutcome.Decline, result.Outcome);
        Assert.Contains(result.Reasons, r => r.Code == "KO04");
    }

    [Fact]
    public async Task Age_at_maturity_over_75_is_declined()
    {
        var facts = Good();
        facts.Age = 67;
        facts.AgeAtMaturity = 77;
        var result = await _engine.DecideAsync(facts);
        Assert.Contains(result.Reasons, r => r.Code == "KO02");
    }

    [Fact]
    public async Task Politically_exposed_person_is_referred()
    {
        var facts = Good();
        facts.PoliticallyExposed = true;
        var result = await _engine.DecideAsync(facts);
        Assert.Equal(DecisionOutcome.Refer, result.Outcome);
        Assert.Contains(result.Reasons, r => r.Code == "RF06");
    }

    [Fact]
    public async Task New_customer_over_15000_is_referred()
    {
        var facts = Good();
        facts.ExistingCustomer = false;
        facts.CustomerMonths = 0;
        facts.KycStatus = "";
        facts.Amount = 20000;
        var result = await _engine.DecideAsync(facts);
        Assert.Equal(DecisionOutcome.Refer, result.Outcome);
        Assert.Contains(result.Reasons, r => r.Code == "RF11");
    }

    [Fact]
    public async Task Every_rule_is_traced()
    {
        var result = await _engine.DecideAsync(Good());
        Assert.Equal(12, result.Trace.Count(t => t.Workflow == "KnockOut"));
        Assert.Equal(11, result.Trace.Count(t => t.Workflow == "Refer"));
    }
}
