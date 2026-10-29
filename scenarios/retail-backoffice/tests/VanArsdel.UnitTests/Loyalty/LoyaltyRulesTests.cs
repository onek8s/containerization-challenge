using FluentAssertions;
using VanArsdel.Customers.Api.Documents;
using VanArsdel.Customers.Api.Domain;
using Xunit;

namespace VanArsdel.UnitTests.Loyalty;

public class LoyaltyRulesTests
{
    [Theory]
    [InlineData(0, LoyaltyTiers.Bronze)]
    [InlineData(299.99, LoyaltyTiers.Bronze)]
    [InlineData(300, LoyaltyTiers.Silver)]
    [InlineData(800, LoyaltyTiers.Gold)]
    [InlineData(2000, LoyaltyTiers.Platinum)]
    public void Tier_follows_rolling_spend(double spend, string tier)
    {
        LoyaltyRules.TierFor((decimal)spend).Should().Be(tier);
    }

    [Theory]
    [InlineData(LoyaltyTiers.Bronze, 59.90, 59)]
    [InlineData(LoyaltyTiers.Gold, 59.90, 89)]
    [InlineData(LoyaltyTiers.Platinum, 59.90, 119)]
    public void Points_depend_on_tier(string tier, double amount, int points)
    {
        LoyaltyRules.PointsFor((decimal)amount, tier).Should().Be(points);
    }

    [Fact]
    public void Swedish_kronor_are_converted_to_euro()
    {
        LoyaltyRules.ToEur(112m, "SEK").Should().Be(10m);
        LoyaltyRules.ToEur(112m, "EUR").Should().Be(112m);
    }

    [Fact]
    public void Same_receipt_is_not_counted_twice()
    {
        var account = new LoyaltyAccount();
        var now = DateTime.UtcNow;

        LoyaltyRules.Apply(account, "Earn", 100m, "FI012", "FI012-000101", now).Should().Be(100);
        LoyaltyRules.Apply(account, "Earn", 100m, "FI012", "FI012-000101", now).Should().Be(0);
        account.Points.Should().Be(100);
    }

    [Fact]
    public void Return_takes_points_back_but_never_below_zero()
    {
        var account = new LoyaltyAccount();
        var now = DateTime.UtcNow;
        LoyaltyRules.Apply(account, "Earn", 50m, "web", "W1", now);

        LoyaltyRules.Apply(account, "Return", 80m, "web", "R1", now).Should().Be(-80);

        account.Points.Should().Be(0);
        LoyaltyRules.RollingSpend(account, now).Should().Be(-30m);
    }

    [Fact]
    public void Rolling_spend_ignores_purchases_older_than_a_year()
    {
        var now = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        var account = new LoyaltyAccount();
        LoyaltyRules.Apply(account, "Earn", 500m, "FI012", "old", now.AddMonths(-13));
        LoyaltyRules.Apply(account, "Earn", 200m, "FI012", "new", now.AddMonths(-1));

        LoyaltyRules.RollingSpend(account, now).Should().Be(200m);
    }

    [Fact]
    public void New_card_numbers_pass_the_check_digit()
    {
        var random = new Random(42);
        for (var i = 0; i < 50; i++)
        {
            var card = LoyaltyRules.NewCardNo(random);
            card.Should().StartWith("6438").And.HaveLength(12);
            LoyaltyRules.IsValidCardNo(card).Should().BeTrue();
        }
    }

    [Theory]
    [InlineData("643800000017", true)]
    [InlineData("643800000018", false)]
    [InlineData("64380000001", false)]
    [InlineData("64380000001X", false)]
    [InlineData(null, false)]
    public void Card_number_check(string cardNo, bool valid)
    {
        LoyaltyRules.IsValidCardNo(cardNo).Should().Be(valid);
    }
}
