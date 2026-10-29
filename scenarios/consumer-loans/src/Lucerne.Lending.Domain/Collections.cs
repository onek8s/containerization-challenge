namespace Lucerne.Lending.Domain;

public enum CollectionStage
{
    None = 0,
    Reminder1 = 1,
    Reminder2 = 2,
    Notice = 3,
    FinalDemand = 4,
    Agency = 5,
    WrittenOff = 9
}

public enum CollectionStatus
{
    Open = 1,
    PaymentPlan = 2,
    Promised = 3,
    HandedOver = 4,
    Closed = 9
}

/// <summary>Collection stages by days past due (collections manual CM-07).</summary>
public sealed class DelinquencyPolicy
{
    public int Reminder1Days { get; init; } = 7;
    public int Reminder2Days { get; init; } = 21;
    public int NoticeDays { get; init; } = 45;
    public int FinalDemandDays { get; init; } = 75;
    public int AgencyDays { get; init; } = 100;
    /// <summary>Debt Collection Act 10 §: max 5 EUR reminder fee, at most 3 per debt.</summary>
    public decimal ReminderFee { get; init; } = 5m;
    public int MaxReminderFees { get; init; } = 3;
    public decimal MinimumArrears { get; init; } = 10m;

    public CollectionStage StageFor(int daysPastDue, decimal arrears)
    {
        if (arrears < MinimumArrears) return CollectionStage.None;
        if (daysPastDue >= AgencyDays) return CollectionStage.Agency;
        if (daysPastDue >= FinalDemandDays) return CollectionStage.FinalDemand;
        if (daysPastDue >= NoticeDays) return CollectionStage.Notice;
        if (daysPastDue >= Reminder2Days) return CollectionStage.Reminder2;
        if (daysPastDue >= Reminder1Days) return CollectionStage.Reminder1;
        return CollectionStage.None;
    }

    public bool ChargesFee(CollectionStage stage, int feesChargedSoFar) =>
        stage is CollectionStage.Reminder1 or CollectionStage.Reminder2 or CollectionStage.Notice or CollectionStage.FinalDemand
        && feesChargedSoFar < MaxReminderFees;

    public string LetterTemplate(CollectionStage stage) => stage switch
    {
        CollectionStage.Reminder1 => "REMINDER1",
        CollectionStage.Reminder2 => "REMINDER2",
        CollectionStage.Notice => "TERMINATION_NOTICE",
        CollectionStage.FinalDemand => "FINAL_DEMAND",
        _ => ""
    };
}

public sealed record PlanInstalment(int Number, DateOnly DueDate, decimal Amount);

public static class PaymentPlanCalculator
{
    /// <summary>Spreads arrears over n months on top of the normal instalment.</summary>
    public static IReadOnlyList<PlanInstalment> Spread(decimal arrears, int months, DateOnly firstDue, decimal regularInstalment)
    {
        if (months is < 1 or > 12) throw new ArgumentOutOfRangeException(nameof(months), "Plans run 1-12 months.");
        var part = Money.Round(arrears / months);
        var result = new List<PlanInstalment>();
        var remaining = arrears;
        for (var i = 1; i <= months; i++)
        {
            var catchUp = i == months ? remaining : part;
            remaining -= catchUp;
            result.Add(new PlanInstalment(i, DueDates.Add(firstDue, i - 1), regularInstalment + catchUp));
        }
        return result;
    }
}
