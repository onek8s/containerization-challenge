namespace Lucerne.Lending.Domain;

public enum Channel
{
    Web = 1,
    Branch = 2,
    Phone = 3,
    Partner = 4
}

public sealed class LoanApplication
{
    public long Id { get; set; }
    public string Reference { get; set; } = "";
    public string ProductCode { get; set; } = "";
    public LoanPurpose Purpose { get; set; }
    public decimal Amount { get; set; }
    public int TermMonths { get; set; }
    public int DueDay { get; set; } = 15;
    public Channel Channel { get; set; } = Channel.Web;
    public ApplicationStatus Status { get; set; }
    public string? CollateralDescription { get; set; }
    public decimal? CollateralValue { get; set; }
    public string? DisbursementIban { get; set; }
    public string? ConsolidatedLoansJson { get; set; }
    public bool ConsentBureau { get; set; }
    public bool ConsentMarketing { get; set; }
    public string? CustomerUserId { get; set; }
    public string? AssignedTo { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime? SubmittedUtc { get; set; }
    public DateTime? DecidedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public byte[]? RowVersion { get; set; }
    public List<Applicant> Applicants { get; set; } = new();

    public Applicant Main => Applicants.First(a => a.IsMain);
    public Applicant? CoApplicant => Applicants.FirstOrDefault(a => !a.IsMain);
}
