namespace Lucerne.Lending.LoanTape;

/// <summary>One loan in the monthly loan tape for the group risk data warehouse (interface spec RDW-CL-07 v4).</summary>
public sealed class LoanTapeRow
{
    public string AccountNumber { get; set; } = "";
    public string CustomerNumber { get; set; } = "";
    public string ProductCode { get; set; } = "";
    public string? ApplicationReference { get; set; }
    public int? Channel { get; set; }
    public DateTime BookedUtc { get; set; }
    public DateTime? DisbursedUtc { get; set; }
    public decimal Amount { get; set; }
    public decimal Balance { get; set; }
    public decimal Arrears { get; set; }
    public int DaysPastDue { get; set; }
    public decimal NominalRatePercent { get; set; }
    public int? TermMonths { get; set; }
    public string? RiskGrade { get; set; }
    public int? Score { get; set; }
    public string CoreStatus { get; set; } = "";
    public bool CoApplicant { get; set; }

    /// <summary>IFRS 9 stage from days past due (1: performing, 2: over 30 dpd, 3: over 90 dpd or terminated).</summary>
    public int Ifrs9Stage => CoreStatus == "TERM" || DaysPastDue > 90 ? 3 : DaysPastDue > 30 ? 2 : 1;
}
