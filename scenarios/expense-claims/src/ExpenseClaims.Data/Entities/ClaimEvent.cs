namespace ExpenseClaims.Data.Entities;

/// <summary>Audit trail of a claim (who did what and when).</summary>
public class ClaimEvent
{
    public long Id { get; set; }
    public int ClaimId { get; set; }
    public DateTime At { get; set; }
    public string Actor { get; set; } = "";
    public string Action { get; set; } = "";
    public string? Details { get; set; }
}
