namespace Lucerne.ProjectSites.Domain
{
    public enum ProjectStage
    {
        Bid = 0,
        Active = 1,
        OnHold = 2,
        Closing = 3,
        Closed = 4,
        Lost = 5
    }

    public enum RagStatus
    {
        NotAssessed = 0,
        Green = 1,
        Amber = 2,
        Red = 3
    }

    public enum DeliverableStatus
    {
        Planned = 0,
        WorkInProgress = 1,
        ForReview = 2,
        Approved = 3,
        Issued = 4,
        Superseded = 5,
        Withdrawn = 6
    }

    public enum ApprovalStatus
    {
        None = 0,
        Pending = 1,
        Approved = 2,
        Rejected = 3
    }

    public enum RiskSeverity
    {
        Low = 1,
        Medium = 2,
        High = 3,
        Critical = 4
    }

    public enum RetentionAction
    {
        Keep = 0,
        DeclareRecord = 1,
        MoveToArchive = 2,
        Delete = 3
    }

    public enum SiteRequestStatus
    {
        New = 0,
        Queued = 1,
        Provisioning = 2,
        Completed = 3,
        Failed = 4,
        Rejected = 5
    }
}
