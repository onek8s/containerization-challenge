using ExpenseClaims.Core.Claims;

namespace ExpenseClaims.Data.Entities;

public class ExpenseClaim
{
    public int Id { get; set; }
    public string Number { get; set; } = "";
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    public string Purpose { get; set; } = "";
    public string CostCenter { get; set; } = "";
    public string? ProjectCode { get; set; }
    public ClaimStatus Status { get; set; }
    public decimal Total { get; set; }

    /// <summary>Part of <see cref="Total"/> that is paid to the employee (lines not paid with a company card).</summary>
    public decimal Reimbursable { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? DecidedAt { get; set; }
    public DateTime? CheckedAt { get; set; }
    public string? CheckedBy { get; set; }
    public int? PaymentRunId { get; set; }
    public PaymentRun? PaymentRun { get; set; }
    public DateTime? PaidAt { get; set; }

    /// <summary>Document number of the journal lines in the ERP general journal.</summary>
    public string? JournalDocumentNo { get; set; }

    public DateTime? ExportedAt { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public List<ClaimLine> Lines { get; set; } = new();
    public List<ClaimApproval> Approvals { get; set; } = new();
    public List<ClaimEvent> Events { get; set; } = new();
}
