namespace Coho.Billing.Lookup.Services;

public class AccountHit
{
    public string AccountNumber { get; set; }
    public string CustomerName { get; set; }
    public string AddressLine1 { get; set; }
    public string City { get; set; }
    public string MeterSerial { get; set; }
    public bool Active { get; set; }
}

public class AccountView
{
    public string AccountNumber { get; set; }
    public string CustomerName { get; set; }
    public string AddressLine1 { get; set; }
    public string AddressLine2 { get; set; }
    public string PostalCode { get; set; }
    public string City { get; set; }
    public string TariffCode { get; set; }
    public string TariffName { get; set; }
    public string CycleCode { get; set; }
    public string CycleName { get; set; }
    public DateTime NextCutOff { get; set; }
    public string MeterSerial { get; set; }
    public string SizeCode { get; set; }
    public byte PaymentMethod { get; set; }
    public string Iban { get; set; }
    public string MandateId { get; set; }
    public decimal AverageDailyUse { get; set; }
    public bool Active { get; set; }
}

public class ReadView
{
    public DateTime ReadDate { get; set; }
    public decimal Reading { get; set; }
    public byte ReadType { get; set; }
    public byte Source { get; set; }
    public string ReaderCode { get; set; }
    public byte Status { get; set; }

    public string TypeText => ReadType switch { 0 => "Actual", 1 => "Customer", _ => "Estimated" };
    public string SourceText => Source switch { 0 => "Handheld", 1 => "AMR", 2 => "Customer", _ => "Billing" };
    public string StatusText => Status switch { 0 => "New", 1 => "Billed", 2 => "Discarded", _ => "Superseded" };
}

public class BillView
{
    public int BillId { get; set; }
    public string BillNumber { get; set; }
    public string AccountNumber { get; set; }
    public int RunId { get; set; }
    public DateTime BillDate { get; set; }
    public DateTime DueDate { get; set; }
    public DateTime PeriodFrom { get; set; }
    public DateTime PeriodTo { get; set; }
    public decimal OpeningReading { get; set; }
    public decimal ClosingReading { get; set; }
    public decimal Consumption { get; set; }
    public bool Estimated { get; set; }
    public decimal NetAmount { get; set; }
    public decimal VatAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string PaymentReference { get; set; }
    public byte Status { get; set; }

    public string StatusText => Status switch { 0 => "Draft", 1 => "Issued", _ => "Not billed" };
}

public class BillLineView
{
    public short LineNumber { get; set; }
    public string Description { get; set; }
    public decimal Quantity { get; set; }
    public string Unit { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Amount { get; set; }
    public decimal VatRate { get; set; }
}

public class ReprintView
{
    public DateTime RequestedAt { get; set; }
    public string RequestedBy { get; set; }
    public string Reason { get; set; }
    public DateTime? ProcessedAt { get; set; }
}
