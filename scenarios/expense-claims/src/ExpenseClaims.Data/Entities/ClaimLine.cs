using ExpenseClaims.Core.Claims;

namespace ExpenseClaims.Data.Entities;

public class ClaimLine
{
    public int Id { get; set; }
    public int ClaimId { get; set; }
    public ExpenseClaim Claim { get; set; } = null!;
    public int LineNumber { get; set; }
    public DateOnly ExpenseDate { get; set; }
    public ExpenseCategory Category { get; set; }
    public string Description { get; set; } = "";
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "EUR";
    public decimal ExchangeRate { get; set; } = 1m;
    public decimal AmountEur { get; set; }
    public decimal? Kilometres { get; set; }
    public string? Route { get; set; }
    public PerDiemType PerDiem { get; set; }
    public string? Country { get; set; }
    public int Attendees { get; set; }

    /// <summary>VAT rate (percent) included in the amount; 0 when the line has no deductible VAT.</summary>
    public decimal VatRate { get; set; }

    public decimal VatAmountEur { get; set; }

    /// <summary>Paid with a company card: booked against the card clearing account, not reimbursed to the employee.</summary>
    public bool PaidByCompany { get; set; }

    public int? CardTransactionId { get; set; }
    public CardTransaction? CardTransaction { get; set; }
    public int? ReceiptId { get; set; }
    public Receipt? Receipt { get; set; }
}
