using Lucerne.Lending.Domain;
using Xunit;

namespace Lucerne.Lending.Tests;

public class DomainTests
{
    [Theory]
    [InlineData("150385-2481", true)]
    [InlineData("110601A139N", true)]
    [InlineData("020790-317C", true)]
    [InlineData("150385-2482", false)]
    [InlineData("320185-2481", false)]
    [InlineData("", false)]
    public void Personal_identity_code_check_character(string code, bool valid) =>
        Assert.Equal(valid, PersonalIdentityCode.IsValid(code));

    [Fact]
    public void Personal_identity_code_century_and_age()
    {
        Assert.True(PersonalIdentityCode.TryParse("110601A139N", out var born, out _));
        Assert.Equal(new DateOnly(2001, 6, 11), born);
        Assert.Equal(67, PersonalIdentityCode.AgeOn("051259-2152", new DateOnly(2027, 1, 1)));
        Assert.Equal("150385-****", PersonalIdentityCode.Mask("150385-2481"));
    }

    [Theory]
    [InlineData("FI21 1234 5600 0007 85", true)]
    [InlineData("FI2112345600000786", false)]
    [InlineData("DE89370400440532013000", true)]
    [InlineData("FI21", false)]
    public void Iban_mod97(string iban, bool valid) => Assert.Equal(valid, Iban.IsValid(iban));

    [Fact]
    public void Iban_format_groups_by_four() => Assert.Equal("FI21 1234 5600 0007 85", Iban.Format("fi2112345600000785"));

    [Fact]
    public void National_reference_7_3_1()
    {
        Assert.Equal("12344", PaymentReference.National("1234"));
        Assert.True(PaymentReference.IsValidNational("12344"));
        Assert.False(PaymentReference.IsValidNational("12345"));
    }

    [Fact]
    public void Rf_reference_is_mod97_valid()
    {
        var rf = PaymentReference.Rf("12344");
        Assert.StartsWith("RF", rf);
        var moved = rf[4..] + "2715" + rf[2..4];
        var rest = moved.Aggregate(0, (r, c) => (r * 10 + (c - '0')) % 97);
        Assert.Equal(1, rest);
    }

    [Fact]
    public void Annuity_payment_and_schedule()
    {
        Assert.Equal(193.33m, Annuity.MonthlyPayment(10000m, 6m, 60));
        Assert.Equal(83.34m, Annuity.MonthlyPayment(1000m, 0m, 12));
        var schedule = Annuity.Schedule(10000m, 6m, 60, new DateOnly(2026, 1, 15));
        Assert.Equal(60, schedule.Count);
        Assert.Equal(0m, schedule[^1].Balance);
        Assert.Equal(10000m, schedule.Sum(l => l.Principal));
    }

    [Fact]
    public void Due_dates_clamp_to_month_end()
    {
        Assert.Equal(new DateOnly(2026, 2, 28), DueDates.Add(new DateOnly(2026, 1, 31), 1));
        var first = DueDates.FirstDueDate(new DateOnly(2026, 3, 20), 15);
        Assert.True(first > new DateOnly(2026, 4, 1) && first.Day == 15);
    }

    [Fact]
    public void Apr_is_above_nominal_with_fees()
    {
        var payment = Annuity.MonthlyPayment(10000m, 6m, 60);
        var aprNoFees = Apr.Calculate(10000m, payment, 60);
        Assert.InRange(aprNoFees, 6.16m, 6.18m);
        var aprWithFees = Apr.Calculate(10000m - 50m, payment + 5m, 60);
        Assert.True(aprWithFees > aprNoFees + 1m);
    }

    [Fact]
    public void Pricing_caps_nominal_rate_and_yearly_fees()
    {
        var product = new LoanProduct
        {
            Code = "PL", Name = "Personal loan", MinAmount = 2000, MaxAmount = 50000, MinTermMonths = 12, MaxTermMonths = 120,
            ReferenceRate = ReferenceRateType.Euribor12M, BaseMarginPercent = 14m, ArrangementFee = 95m, MonthlyFee = 7.5m
        };
        var quote = Pricing.Quote(product, 10000m, 60, RiskGrade.E, 3.2m);
        Assert.Equal(20m, quote.NominalRatePercent);
        Assert.Equal(60m, quote.ArrangementFee); // 150 - 12 * 7.50
        var a = Pricing.Quote(product, 10000m, 60, RiskGrade.A, 3.2m);
        Assert.Equal(17.2m, a.NominalRatePercent);
    }

    [Theory]
    [InlineData(3, 50, CollectionStage.None)]
    [InlineData(8, 50, CollectionStage.Reminder1)]
    [InlineData(30, 50, CollectionStage.Reminder2)]
    [InlineData(50, 50, CollectionStage.Notice)]
    [InlineData(80, 50, CollectionStage.FinalDemand)]
    [InlineData(120, 50, CollectionStage.Agency)]
    [InlineData(120, 5, CollectionStage.None)]
    public void Delinquency_stage_by_days_past_due(int dpd, decimal arrears, CollectionStage expected) =>
        Assert.Equal(expected, new DelinquencyPolicy().StageFor(dpd, arrears));

    [Fact]
    public void Reminder_fees_limited_to_three()
    {
        var policy = new DelinquencyPolicy();
        Assert.True(policy.ChargesFee(CollectionStage.Reminder2, 2));
        Assert.False(policy.ChargesFee(CollectionStage.Notice, 3));
        Assert.False(policy.ChargesFee(CollectionStage.Agency, 0));
    }

    [Fact]
    public void Payment_plan_spreads_arrears_on_top_of_instalment()
    {
        var plan = PaymentPlanCalculator.Spread(301m, 3, new DateOnly(2026, 5, 15), 200m);
        Assert.Equal(3, plan.Count);
        Assert.Equal(200m * 3 + 301m, plan.Sum(p => p.Amount));
        Assert.Equal(new DateOnly(2026, 7, 15), plan[2].DueDate);
    }

    [Fact]
    public void Status_rules()
    {
        Assert.True(ApplicationStatusRules.CanMove(ApplicationStatus.OfferIssued, ApplicationStatus.OfferAccepted));
        Assert.False(ApplicationStatusRules.CanMove(ApplicationStatus.Declined, ApplicationStatus.Booked));
        Assert.Throws<InvalidOperationException>(() => ApplicationStatusRules.EnsureCanMove(ApplicationStatus.Disbursed, ApplicationStatus.Cancelled));
        Assert.True(ApplicationStatusRules.CustomerCanCancel(ApplicationStatus.OfferIssued));
        Assert.False(ApplicationStatusRules.CustomerCanCancel(ApplicationStatus.Signed));
    }

    [Fact]
    public void References()
    {
        Assert.Equal("LA-2026-000042", ReferenceGenerator.Application(2026, 42));
        Assert.Equal("CC-0000007", ReferenceGenerator.CollectionCase(7));
    }
}
