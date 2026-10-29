using Humongous.Quoting.Domain;

namespace Humongous.Quoting.Tests;

public class PremiumTests
{
    [Fact]
    public void Grosses_up_for_commission_and_adds_tax_and_fee()
    {
        var rating = TestRisks.Rated((SectionCode.MaterialDamage, 400m), (SectionCode.PublicLiability, 300m));

        var premium = PremiumCalculator.Calculate(rating, TestRisks.Product, 0.20m);

        Assert.Equal(700m, premium.NetPremium);
        Assert.Equal(875m, premium.GrossPremium); // 500 + 375
        Assert.Equal(175m, premium.Commission);
        Assert.Equal(105m, premium.Ipt);
        Assert.Equal(25m, premium.Fee);
        Assert.Equal(1005m, premium.Total);
    }

    [Fact]
    public void Applies_section_minimum_premium()
    {
        var rating = TestRisks.Rated((SectionCode.EmployersLiability, 60m));

        var premium = PremiumCalculator.Calculate(rating, TestRisks.Product, 0m);

        var el = Assert.Single(premium.Sections);
        Assert.Equal(140m, el.Net);
        Assert.Equal(80m, el.MinimumApplied);
    }

    [Fact]
    public void Applies_underwriter_loading_per_section()
    {
        var rating = TestRisks.Rated((SectionCode.MaterialDamage, 1000m), (SectionCode.PublicLiability, 1000m));
        var loadings = new Dictionary<SectionCode, decimal> { [SectionCode.MaterialDamage] = 25m };

        var premium = PremiumCalculator.Calculate(rating, TestRisks.Product, 0m, loadings);

        Assert.Equal(1250m, premium.Sections[0].Net);
        Assert.Equal(1000m, premium.Sections[1].Net);
    }

    [Fact]
    public void Rejects_commission_above_product_maximum()
    {
        var rating = TestRisks.Rated((SectionCode.MaterialDamage, 1000m));
        Assert.Throws<ArgumentOutOfRangeException>(() => PremiumCalculator.Calculate(rating, TestRisks.Product, 0.30m));
    }

    [Fact]
    public void Pro_rata_adjustment_for_half_the_period()
    {
        var start = new DateOnly(2024, 1, 1);
        var end = new DateOnly(2024, 12, 31); // 366 days
        var effective = new DateOnly(2024, 7, 2); // 183 days remaining

        Assert.Equal(500m, ProRata.Adjustment(1000m, 2000m, start, end, effective));
        Assert.Equal(-500m, ProRata.Adjustment(2000m, 1000m, start, end, effective));
    }

    [Fact]
    public void Cancellation_in_first_14_days_returns_full_premium()
    {
        var start = new DateOnly(2024, 5, 1);
        Assert.Equal(1200m, ProRata.CancellationReturn(1200m, start, start.AddYears(1).AddDays(-1), start.AddDays(10)));
        Assert.True(ProRata.CancellationReturn(1200m, start, start.AddYears(1).AddDays(-1), start.AddDays(100)) < 1200m);
    }

    [Fact]
    public void Effective_date_outside_period_is_rejected()
    {
        var start = new DateOnly(2024, 5, 1);
        Assert.Throws<ArgumentOutOfRangeException>(() => ProRata.Factor(start, start.AddYears(1), start.AddDays(-1)));
    }
}
