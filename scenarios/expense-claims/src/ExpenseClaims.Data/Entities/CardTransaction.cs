namespace ExpenseClaims.Data.Entities;

/// <summary>Company card holder. Cards are issued by the card company and linked to employees by finance.</summary>
public class CompanyCard
{
    public int Id { get; set; }

    /// <summary>Masked card number as in the card company's file, for example 552235******4410.</summary>
    public string MaskedNumber { get; set; } = "";

    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    public bool Active { get; set; } = true;
}

/// <summary>Transaction from the card company's daily transaction file.</summary>
public class CardTransaction
{
    public int Id { get; set; }

    /// <summary>Card company's transaction reference; unique across files.</summary>
    public string Reference { get; set; } = "";

    public int CompanyCardId { get; set; }
    public CompanyCard CompanyCard { get; set; } = null!;
    public DateOnly TransactionDate { get; set; }
    public DateOnly PostingDate { get; set; }
    public string Merchant { get; set; } = "";
    public string? MerchantCity { get; set; }
    public string? MerchantCountry { get; set; }

    /// <summary>ISO 18245 merchant category code.</summary>
    public string MerchantCategoryCode { get; set; } = "";

    public decimal OriginalAmount { get; set; }
    public string OriginalCurrency { get; set; } = "EUR";
    public decimal BilledAmountEur { get; set; }
    public string SourceFile { get; set; } = "";
    public DateTime ImportedAt { get; set; }
}
