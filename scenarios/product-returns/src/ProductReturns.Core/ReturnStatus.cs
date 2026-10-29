namespace ProductReturns.Core
{
    public enum ReturnStatus
    {
        Submitted = 0,
        Approved = 1,
        Rejected = 2,
        LabelIssued = 3,
        Received = 4,
        Inspected = 5,
        RefundExported = 6,
        Closed = 7,
        Cancelled = 8
    }

    public enum ItemCondition
    {
        NotInspected = 0,
        AsNew = 1,
        Opened = 2,
        Used = 3,
        Unsellable = 4
    }
}
