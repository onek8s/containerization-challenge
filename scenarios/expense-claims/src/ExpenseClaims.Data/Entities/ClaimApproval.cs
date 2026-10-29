namespace ExpenseClaims.Data.Entities;

public enum ApprovalDecision
{
    Pending = 0,
    Approved = 1,
    Rejected = 2,
    Returned = 3
}

public class ClaimApproval
{
    public int Id { get; set; }
    public int ClaimId { get; set; }
    public ExpenseClaim Claim { get; set; } = null!;
    public int Level { get; set; }
    public int ApproverId { get; set; }
    public Employee Approver { get; set; } = null!;
    public ApprovalDecision Decision { get; set; }
    public string? Comment { get; set; }
    public DateTime? DecidedAt { get; set; }
    public DateTime? RemindedAt { get; set; }
}
