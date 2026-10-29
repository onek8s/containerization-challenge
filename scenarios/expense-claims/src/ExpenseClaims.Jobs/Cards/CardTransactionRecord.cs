using CsvHelper.Configuration.Attributes;

namespace ExpenseClaims.Jobs.Cards;

/// <summary>One row of the card company's corporate transaction file.</summary>
public sealed class CardTransactionRecord
{
    [Name("TransactionId")] public string Reference { get; set; } = "";
    [Name("CardNumber")] public string MaskedNumber { get; set; } = "";
    [Name("TransactionDate")] [Format("dd.MM.yyyy")] public DateTime TransactionDate { get; set; }
    [Name("PostingDate")] [Format("dd.MM.yyyy")] public DateTime PostingDate { get; set; }
    [Name("MerchantName")] public string Merchant { get; set; } = "";
    [Name("MerchantCity")] public string? MerchantCity { get; set; }
    [Name("MerchantCountry")] public string? MerchantCountry { get; set; }
    [Name("MCC")] public string MerchantCategoryCode { get; set; } = "";
    [Name("OriginalAmount")] public decimal OriginalAmount { get; set; }
    [Name("OriginalCurrency")] public string OriginalCurrency { get; set; } = "EUR";
    [Name("BilledAmount")] public decimal BilledAmount { get; set; }
    [Name("BilledCurrency")] public string BilledCurrency { get; set; } = "EUR";
}
