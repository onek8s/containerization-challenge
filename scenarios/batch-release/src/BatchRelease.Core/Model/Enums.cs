namespace BatchRelease.Core.Model;

public enum BatchStatus
{
    Received = 0,
    UnderReview = 1,
    Reviewed = 2,
    Released = 3,
    Rejected = 4,
    OnHold = 5,
}

public enum StepStatus
{
    Open = 0,
    Complete = 1,
    NotApplicable = 2,
}

public enum ResultOutcome
{
    Pending = 0,
    Pass = 1,
    Fail = 2,
}

public enum DeviationClass
{
    Minor = 0,
    Major = 1,
    Critical = 2,
}

public enum DeviationStatus
{
    Open = 0,
    ImpactAssessed = 1,
    Closed = 2,
}

public enum ProductImpact
{
    NotAssessed = 0,
    None = 1,
    Possible = 2,
    Confirmed = 3,
}

public enum DecisionKind
{
    Review = 1,
    Release = 2,
    Reject = 3,
    Hold = 4,
    Resume = 5,
}
