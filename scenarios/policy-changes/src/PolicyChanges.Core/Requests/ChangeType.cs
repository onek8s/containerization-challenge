namespace PolicyChanges.Core.Requests;

public enum ChangeType
{
    AddressChange,
    PaymentMethodChange,
    BeneficiaryChange,
    Cancellation,
}

public enum ChangeStatus
{
    /// <summary>Waiting for a desk station.</summary>
    Queued,

    /// <summary>Claimed by a desk station.</summary>
    InProgress,

    /// <summary>Endorsement created in PolicyDesk.</summary>
    Applied,

    /// <summary>PolicyDesk refused the change (business rule).</summary>
    Rejected,

    /// <summary>Retries used up, a service lead must decide.</summary>
    NeedsReview,

    /// <summary>Withdrawn by the submitter or a service lead.</summary>
    Withdrawn,
}

public enum ChangeChannel
{
    Portal,
    Partner,
}
