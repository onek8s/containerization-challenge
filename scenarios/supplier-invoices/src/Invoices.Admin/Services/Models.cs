using Wingtip.Invoices.Core;

namespace Wingtip.Invoices.Admin.Services;

public class InvoiceRow
{
    public long InvoiceId { get; set; }
    public long FileId { get; set; }
    public int SupplierId { get; set; }
    public string SupplierCode { get; set; }
    public string SupplierName { get; set; }
    public string VendorNumber { get; set; }
    public string InvoiceNumber { get; set; }
    public short DocumentType { get; set; }
    public DateTime InvoiceDate { get; set; }
    public DateTime? DueDate { get; set; }
    public string Currency { get; set; }
    public decimal NetAmount { get; set; }
    public decimal VatAmount { get; set; }
    public decimal GrossAmount { get; set; }
    public string PurchaseOrderNumber { get; set; }
    public string PaymentReference { get; set; }
    public byte Status { get; set; }
    public string MatchSummary { get; set; }
    public DateTime ReceivedAt { get; set; }
    public string ReleasedBy { get; set; }
    public DateTime? ReleasedAt { get; set; }
    public long? BatchId { get; set; }
    public string VoucherNumber { get; set; }
    public DateTime? PostedAt { get; set; }
    public string ErpMessage { get; set; }
    public int OpenIssues { get; set; }

    public InvoiceStatus StatusValue => (InvoiceStatus)Status;
    public bool IsCredit => DocumentType == (short)Core.DocumentType.CreditNote;
    public bool CanAct => StatusValue is InvoiceStatus.Matched or InvoiceStatus.Exception or InvoiceStatus.PostingFailed;
}

public class LineRow
{
    public int LineNumber { get; set; }
    public string ItemNumber { get; set; }
    public string SupplierItemNumber { get; set; }
    public string Description { get; set; }
    public decimal Quantity { get; set; }
    public string Unit { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal NetAmount { get; set; }
    public decimal VatRate { get; set; }
    public string PurchaseOrderNumber { get; set; }
    public int? PurchaseOrderLine { get; set; }
    public string Account { get; set; }
}

public class IssueRow
{
    public string Code { get; set; }
    public string Message { get; set; }
    public bool Resolved { get; set; }
}

public class HistoryRow
{
    public DateTime At { get; set; }
    public string UserName { get; set; }
    public string Action { get; set; }
    public string Comment { get; set; }
}

public class FileRow
{
    public long FileId { get; set; }
    public string SupplierCode { get; set; }
    public string ChannelType { get; set; }
    public string FileName { get; set; }
    public long SizeBytes { get; set; }
    public string ArchivePath { get; set; }
    public DateTime ReceivedAt { get; set; }
    public string Status { get; set; }
    public int Attempts { get; set; }
    public string LastError { get; set; }
    public int Invoices { get; set; }
}

public class SupplierRow
{
    public int SupplierId { get; set; }
    public string Code { get; set; }
    public string Name { get; set; }
    public string VatNumber { get; set; }
    public string VendorNumber { get; set; }
    public int CodePage { get; set; }
    public decimal? PriceTolerancePercent { get; set; }
    public bool TwoWayMatch { get; set; }
    public string CostCenter { get; set; }
    public string ContactEmail { get; set; }
    public bool Active { get; set; }
    public string ModifiedBy { get; set; }
    public DateTime ModifiedAt { get; set; }
}

public class ChannelRow
{
    public int ChannelId { get; set; }
    public int SupplierId { get; set; }
    public string SupplierCode { get; set; }
    public string ChannelType { get; set; }
    public string Location { get; set; }
    public string FilePattern { get; set; }
    public bool Active { get; set; }
    public DateTime? LastPolledAt { get; set; }
    public string LastError { get; set; }
    public DateTime? LastErrorAt { get; set; }
}

public class BatchRow
{
    public long BatchId { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Status { get; set; }
    public string FileName { get; set; }
    public int InvoiceCount { get; set; }
    public decimal? GrossTotal { get; set; }
    public DateTime? WrittenAt { get; set; }
    public string AckFile { get; set; }
    public DateTime? AckAt { get; set; }
    public int Posted { get; set; }
    public int Rejected { get; set; }
}

public class StatusCount
{
    public byte Status { get; set; }
    public int Count { get; set; }
    public decimal Gross { get; set; }
}

public class IssueCount
{
    public string Code { get; set; }
    public int Count { get; set; }
    public DateTime Oldest { get; set; }
}

public static class StatusText
{
    public static string For(InvoiceStatus status) => status switch
    {
        InvoiceStatus.Received => "Received",
        InvoiceStatus.Matched => "Matched",
        InvoiceStatus.Exception => "Exception",
        InvoiceStatus.Released => "Released",
        InvoiceStatus.Posting => "Sent to ERP",
        InvoiceStatus.Posted => "Posted",
        InvoiceStatus.PostingFailed => "ERP rejected",
        InvoiceStatus.Rejected => "Rejected",
        _ => status.ToString()
    };

    public static string Css(InvoiceStatus status) => status switch
    {
        InvoiceStatus.Exception or InvoiceStatus.PostingFailed => "bad",
        InvoiceStatus.Posted => "good",
        InvoiceStatus.Rejected => "muted",
        _ => "info"
    };
}
