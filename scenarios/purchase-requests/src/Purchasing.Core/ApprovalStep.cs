namespace Contoso.Purchasing.Core
{
    public enum ApprovalStep : byte
    {
        Manager = 1,
        Finance = 2,
        Director = 3
    }

    public enum ApprovalDecision : byte
    {
        Approved = 1,
        Rejected = 2
    }
}
