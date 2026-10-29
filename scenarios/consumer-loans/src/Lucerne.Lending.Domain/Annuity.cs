namespace Lucerne.Lending.Domain;

public sealed record ScheduleLine(int Number, DateOnly DueDate, decimal Payment, decimal Interest, decimal Principal, decimal Fee, decimal Balance);

/// <summary>Annuity (equal instalment) loans, 30E/360 interest as booked in the core ledger.</summary>
public static class Annuity
{
    public static decimal MonthlyPayment(decimal principal, decimal annualRatePercent, int months)
    {
        if (months <= 0) throw new ArgumentOutOfRangeException(nameof(months));
        if (principal <= 0) return 0m;
        if (annualRatePercent == 0) return Money.RoundUpToCent(principal / months);
        var r = (double)annualRatePercent / 100d / 12d;
        var payment = (double)principal * r / (1 - Math.Pow(1 + r, -months));
        return Money.RoundUpToCent((decimal)payment);
    }

    public static IReadOnlyList<ScheduleLine> Schedule(decimal principal, decimal annualRatePercent, int months,
        DateOnly firstDueDate, decimal monthlyFee = 0m)
    {
        var payment = MonthlyPayment(principal, annualRatePercent, months);
        var lines = new List<ScheduleLine>(months);
        var balance = principal;
        var monthlyRate = annualRatePercent / 100m / 12m;
        for (var n = 1; n <= months; n++)
        {
            var interest = Money.Round(balance * monthlyRate);
            var principalPart = n == months ? balance : Math.Min(balance, payment - interest);
            balance -= principalPart;
            lines.Add(new ScheduleLine(n, DueDates.Add(firstDueDate, n - 1), principalPart + interest + monthlyFee,
                interest, principalPart, monthlyFee, balance));
        }
        return lines;
    }
}

public static class DueDates
{
    /// <summary>Adds months keeping the due day; the 29th-31st move to the last day of short months.</summary>
    public static DateOnly Add(DateOnly first, int months)
    {
        var target = first.AddMonths(months);
        var lastDay = DateTime.DaysInMonth(target.Year, target.Month);
        return new DateOnly(target.Year, target.Month, Math.Min(first.Day, lastDay));
    }

    /// <summary>First due date is the chosen due day of the month after disbursement, at least 20 days away.</summary>
    public static DateOnly FirstDueDate(DateOnly disbursement, int dueDay)
    {
        if (dueDay is < 1 or > 28) throw new ArgumentOutOfRangeException(nameof(dueDay), "Due day must be 1-28.");
        var candidate = new DateOnly(disbursement.Year, disbursement.Month, dueDay).AddMonths(1);
        if (candidate.DayNumber - disbursement.DayNumber < 20) candidate = candidate.AddMonths(1);
        return candidate;
    }
}
