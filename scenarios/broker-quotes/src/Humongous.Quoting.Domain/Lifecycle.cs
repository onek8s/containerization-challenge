namespace Humongous.Quoting.Domain;

public enum QuoteStatus
{
    Draft,
    Rating,
    Quoted,
    Referred,
    Accepted,
    Declined,
    BindRequested,
    Bound,
    Expired,
    NotTakenUp,
    RatingFailed
}

public enum PolicyStatus
{
    Live,
    Cancelled,
    Lapsed,
    Renewed
}

public enum TransactionType
{
    NewBusiness,
    MidTermAdjustment,
    Renewal,
    Cancellation
}

public enum ReferralDecision
{
    Accept,
    Decline
}

public static class QuoteLifecycle
{
    public const int ValidityDays = 30;

    private static readonly Dictionary<QuoteStatus, QuoteStatus[]> Allowed = new()
    {
        [QuoteStatus.Draft] = [QuoteStatus.Rating],
        [QuoteStatus.Rating] = [QuoteStatus.Quoted, QuoteStatus.Referred, QuoteStatus.Declined, QuoteStatus.RatingFailed],
        [QuoteStatus.RatingFailed] = [QuoteStatus.Rating],
        [QuoteStatus.Quoted] = [QuoteStatus.Rating, QuoteStatus.BindRequested, QuoteStatus.Expired, QuoteStatus.NotTakenUp],
        [QuoteStatus.Referred] = [QuoteStatus.Rating, QuoteStatus.Accepted, QuoteStatus.Declined, QuoteStatus.Expired],
        [QuoteStatus.Accepted] = [QuoteStatus.BindRequested, QuoteStatus.Expired, QuoteStatus.NotTakenUp],
        [QuoteStatus.BindRequested] = [QuoteStatus.Bound, QuoteStatus.Quoted, QuoteStatus.Accepted],
        [QuoteStatus.Declined] = [],
        [QuoteStatus.Bound] = [],
        [QuoteStatus.Expired] = [],
        [QuoteStatus.NotTakenUp] = []
    };

    public static bool CanMove(QuoteStatus from, QuoteStatus to) => Allowed[from].Contains(to);

    public static void EnsureCanMove(QuoteStatus from, QuoteStatus to)
    {
        if (!CanMove(from, to))
            throw new InvalidOperationException($"A quote cannot move from {from} to {to}.");
    }

    public static bool IsBindable(QuoteStatus status) => status is QuoteStatus.Quoted or QuoteStatus.Accepted;

    public static string TransactionCode(TransactionType type) => type switch
    {
        TransactionType.NewBusiness => "NB",
        TransactionType.MidTermAdjustment => "MTA",
        TransactionType.Renewal => "REN",
        TransactionType.Cancellation => "CAN",
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };
}

public static class References
{
    public static string Quote(int year, long sequence) => $"HQ{year % 100:00}-{sequence:000000}";

    public static string Policy(long sequence, DateOnly inception) => $"HBC/{sequence:000000}/{inception.Year % 100:00}";

    public static string RenewedPolicy(string policyNumber, DateOnly inception)
    {
        var slash = policyNumber.LastIndexOf('/');
        return slash < 0 ? policyNumber : $"{policyNumber[..slash]}/{inception.Year % 100:00}";
    }
}
