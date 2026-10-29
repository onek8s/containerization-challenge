namespace Proseware.ControlTower.Domain.Parties;

public class Customer
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string? VatNumber { get; set; }
    /// <summary>EORI number used on customs declarations.</summary>
    public string? Eori { get; set; }
    public string CountryCode { get; set; } = "FI";
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string InvoiceCurrency { get; set; } = "EUR";
    public bool PortalEnabled { get; set; }
    public bool CustomsBrokerageAgreed { get; set; }
    /// <summary>Agreed delivery SLA in working days for domestic road shipments.</summary>
    public int DomesticSlaDays { get; set; } = 2;
    public decimal DiscountPercent { get; set; }
    public DateTime CreatedUtc { get; set; }
}
