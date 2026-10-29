namespace Lucerne.Lending.Domain;

public enum ApplicationStatus
{
    Draft = 0,
    Submitted = 10,
    Scoring = 20,
    Referred = 30,
    Approved = 40,
    Declined = 50,
    OfferIssued = 60,
    OfferAccepted = 70,
    AwaitingSignature = 80,
    Signed = 90,
    Booked = 100,
    Disbursed = 110,
    Cancelled = 120,
    Expired = 130,
    Failed = 140
}

public static class ApplicationStatusRules
{
    private static readonly Dictionary<ApplicationStatus, ApplicationStatus[]> Allowed = new()
    {
        [ApplicationStatus.Draft] = new[] { ApplicationStatus.Submitted, ApplicationStatus.Cancelled },
        [ApplicationStatus.Submitted] = new[] { ApplicationStatus.Scoring, ApplicationStatus.Cancelled },
        [ApplicationStatus.Scoring] = new[] { ApplicationStatus.Approved, ApplicationStatus.Referred, ApplicationStatus.Declined, ApplicationStatus.Failed },
        [ApplicationStatus.Failed] = new[] { ApplicationStatus.Submitted, ApplicationStatus.Cancelled },
        [ApplicationStatus.Referred] = new[] { ApplicationStatus.Approved, ApplicationStatus.Declined, ApplicationStatus.Cancelled },
        [ApplicationStatus.Approved] = new[] { ApplicationStatus.OfferIssued, ApplicationStatus.Cancelled },
        [ApplicationStatus.OfferIssued] = new[] { ApplicationStatus.OfferAccepted, ApplicationStatus.Cancelled, ApplicationStatus.Expired },
        [ApplicationStatus.OfferAccepted] = new[] { ApplicationStatus.AwaitingSignature, ApplicationStatus.Cancelled },
        [ApplicationStatus.AwaitingSignature] = new[] { ApplicationStatus.Signed, ApplicationStatus.Cancelled, ApplicationStatus.Expired },
        [ApplicationStatus.Signed] = new[] { ApplicationStatus.Booked, ApplicationStatus.Cancelled },
        [ApplicationStatus.Booked] = new[] { ApplicationStatus.Disbursed },
        [ApplicationStatus.Disbursed] = Array.Empty<ApplicationStatus>(),
        [ApplicationStatus.Declined] = Array.Empty<ApplicationStatus>(),
        [ApplicationStatus.Cancelled] = Array.Empty<ApplicationStatus>(),
        [ApplicationStatus.Expired] = Array.Empty<ApplicationStatus>()
    };

    public static bool CanMove(ApplicationStatus from, ApplicationStatus to) =>
        Allowed.TryGetValue(from, out var targets) && targets.Contains(to);

    public static void EnsureCanMove(ApplicationStatus from, ApplicationStatus to)
    {
        if (!CanMove(from, to))
            throw new InvalidOperationException($"Application cannot move from {from} to {to}.");
    }

    public static bool IsOpen(ApplicationStatus status) => status is not (ApplicationStatus.Disbursed
        or ApplicationStatus.Declined or ApplicationStatus.Cancelled or ApplicationStatus.Expired);

    public static bool CustomerCanCancel(ApplicationStatus status) => status is ApplicationStatus.Draft
        or ApplicationStatus.Submitted or ApplicationStatus.Referred or ApplicationStatus.Approved
        or ApplicationStatus.OfferIssued or ApplicationStatus.OfferAccepted or ApplicationStatus.AwaitingSignature;
}
