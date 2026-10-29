namespace Lucerne.Lending.Domain;

/// <summary>
/// Annual percentage rate of charge (todellinen vuosikorko) as in the Consumer Credit Directive,
/// Annex I: time in years from the drawdown, a month counted as 1/12 of a year.
/// </summary>
public static class Apr
{
    public static decimal Calculate(decimal netAmountPaidToCustomer, decimal monthlyPaymentInclFees, int months,
        decimal finalExtra = 0m)
    {
        if (netAmountPaidToCustomer <= 0 || months <= 0) return 0m;
        double Npv(double rate)
        {
            var sum = 0d;
            for (var k = 1; k <= months; k++)
            {
                var t = k / 12d;
                var flow = (double)monthlyPaymentInclFees + (k == months ? (double)finalExtra : 0d);
                sum += flow / Math.Pow(1 + rate, t);
            }
            return sum - (double)netAmountPaidToCustomer;
        }

        double lo = -0.99, hi = 10;
        if (Npv(0) < 0) return 0m;
        for (var i = 0; i < 200; i++)
        {
            var mid = (lo + hi) / 2;
            if (Npv(mid) > 0) lo = mid; else hi = mid;
        }
        return Math.Round((decimal)((lo + hi) / 2 * 100), 2, MidpointRounding.AwayFromZero);
    }
}
