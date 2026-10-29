using Humongous.Quoting.Domain;

namespace Humongous.Quoting.Tests;

public class UnderwritingTests
{
    private static readonly DateOnly Today = new(2024, 6, 1);

    [Fact]
    public void Standard_cafe_is_not_referred()
    {
        Assert.Empty(ReferralRules.Evaluate(TestRisks.Cafe(), TestRisks.Product, Today));
        Assert.Empty(ReferralRules.Declines(TestRisks.Cafe()));
    }

    [Fact]
    public void Referred_trade_refers()
    {
        var risk = TestRisks.Cafe() with { Insured = TestRisks.Cafe().Insured with { TradeCode = "56302" } };
        Assert.Contains(ReferralRules.Evaluate(risk, TestRisks.Product, Today), r => r.Code == "T01");
    }

    [Fact]
    public void Declined_trade_declines()
    {
        var risk = TestRisks.Cafe() with { Insured = TestRisks.Cafe().Insured with { TradeCode = "20510" } };
        Assert.Contains(ReferralRules.Declines(risk), r => r.Code == "D01");
    }

    [Fact]
    public void Sums_insured_over_authority_refer()
    {
        var risk = TestRisks.Cafe() with { MaterialDamage = new MaterialDamageCover { Buildings = 5_500_000m } };
        Assert.Contains(ReferralRules.Evaluate(risk, TestRisks.Product, Today), r => r.Code == "S01");
    }

    [Fact]
    public void Three_recent_claims_refer_but_old_claims_do_not()
    {
        var recent = Enumerable.Range(1, 3).Select(i => new ClaimRecord { Date = Today.AddMonths(-i * 6), Amount = 1_000m, Description = "Escape of water" }).ToList();
        var old = recent.Select(c => c with { Date = c.Date.AddYears(-6) }).ToList();

        Assert.Contains(ReferralRules.Evaluate(TestRisks.Cafe() with { Claims = recent }, TestRisks.Product, Today), r => r.Code == "C01");
        Assert.DoesNotContain(ReferralRules.Evaluate(TestRisks.Cafe() with { Claims = old }, TestRisks.Product, Today), r => r.Code == "C01");
    }

    [Fact]
    public void Unoccupied_and_thatched_premises_refer()
    {
        var risk = TestRisks.Cafe() with { Premises = new PremisesDetails { YearBuilt = 1750, Occupied = false, Roof = RoofType.Thatch } };
        var codes = ReferralRules.Evaluate(risk, TestRisks.Product, Today).Select(r => r.Code).ToList();
        Assert.Contains("P01", codes);
        Assert.Contains("P02", codes);
    }

    [Fact]
    public void Outcome_combines_platform_rules_and_rating_table_messages()
    {
        var rating = TestRisks.Rated((SectionCode.MaterialDamage, 500m)) with
        {
            Referrals = [new RatingMessage("R10", "Postcode district in flood zone 4")]
        };

        var outcome = UnderwritingOutcome.Build(rating, TestRisks.Product, TestRisks.Cafe(), 0.2m, Today);

        Assert.Equal(QuoteStatus.Referred, outcome.Status);
        Assert.Contains(outcome.Messages, m => m.Code == "R10" && m.Source == "RateCore");
    }

    [Fact]
    public void Decline_wins_over_referral()
    {
        var rating = TestRisks.Rated((SectionCode.MaterialDamage, 500m)) with
        {
            Referrals = [new RatingMessage("R10", "Flood")],
            Declines = [new RatingMessage("D10", "Trade declined by the rating tables")]
        };

        var outcome = UnderwritingOutcome.Build(rating, TestRisks.Product, TestRisks.Cafe(), 0.2m, Today);

        Assert.Equal(QuoteStatus.Declined, outcome.Status);
        Assert.Single(outcome.Messages);
    }

    [Theory]
    [InlineData(QuoteStatus.Quoted, QuoteStatus.BindRequested, true)]
    [InlineData(QuoteStatus.Referred, QuoteStatus.BindRequested, false)]
    [InlineData(QuoteStatus.Accepted, QuoteStatus.BindRequested, true)]
    [InlineData(QuoteStatus.Bound, QuoteStatus.Rating, false)]
    [InlineData(QuoteStatus.RatingFailed, QuoteStatus.Rating, true)]
    public void Quote_lifecycle_transitions(QuoteStatus from, QuoteStatus to, bool allowed) =>
        Assert.Equal(allowed, QuoteLifecycle.CanMove(from, to));
}
