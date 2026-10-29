namespace Contoso.Purchasing.Core
{
    public enum RequestStatus : byte
    {
        Draft = 0,
        InApproval = 1,
        Approved = 2,
        Rejected = 3,
        Ordered = 4,
        Cancelled = 5
    }
}
