namespace Lucerne.Lending.Domain;

public enum RiskGrade
{
    A = 1,
    B = 2,
    C = 3,
    D = 4,
    E = 5,
    /// <summary>Not graded (declined or no score).</summary>
    X = 9
}

public sealed record PriceQuote(decimal ReferenceRatePercent, decimal MarginPercent, decimal NominalRatePercent,
    decimal MonthlyPayment, decimal ArrangementFee, decimal MonthlyFee, decimal AprPercent, decimal TotalRepayable);

public static class Pricing
{
    /// <summary>Statutory cap for consumer credit nominal rate (Kuluttajansuojalaki 7:17a, from 2019-09-01: 20 %).</summary>
    public const decimal NominalRateCap = 20m;
    /// <summary>Costs other than interest may not exceed 150 EUR per year (KSL 7:17a).</summary>
    public const decimal YearlyFeeCap = 150m;

    public static decimal GradeAddOn(RiskGrade grade) => grade switch
    {
        RiskGrade.A => 0m,
        RiskGrade.B => 1.5m,
        RiskGrade.C => 3.0m,
        RiskGrade.D => 5.0m,
        RiskGrade.E => 7.5m,
        _ => throw new ArgumentOutOfRangeException(nameof(grade))
    };

    public static PriceQuote Quote(LoanProduct product, decimal amount, int termMonths, RiskGrade grade,
        decimal referenceRatePercent, decimal marginOverride = -1m)
    {
        var refRate = product.ReferenceRate == ReferenceRateType.Fixed ? 0m : Math.Max(0m, referenceRatePercent);
        var margin = marginOverride >= 0 ? marginOverride : product.BaseMarginPercent + GradeAddOn(grade);
        var nominal = Math.Min(NominalRateCap, refRate + margin);
        var monthlyFee = product.MonthlyFee;
        var arrangement = product.ArrangementFee;
        var firstYearFees = arrangement + monthlyFee * Math.Min(12, termMonths);
        if (firstYearFees > YearlyFeeCap)
        {
            arrangement = Math.Max(0, YearlyFeeCap - monthlyFee * Math.Min(12, termMonths));
        }
        var payment = Annuity.MonthlyPayment(amount, nominal, termMonths);
        var apr = Apr.Calculate(amount - arrangement, payment + monthlyFee, termMonths);
        var total = Money.Round((payment + monthlyFee) * termMonths + arrangement);
        return new PriceQuote(refRate, margin, nominal, payment, arrangement, monthlyFee, apr, total);
    }
}
