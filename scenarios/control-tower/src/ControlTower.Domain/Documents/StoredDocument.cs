namespace Proseware.ControlTower.Domain.Documents;

public enum DocumentKind
{
    Cmr = 1,
    HouseBillOfLading = 2,
    HouseAirWaybill = 3,
    DeliveryNote = 4,
    CommercialInvoice = 5,
    PackingList = 6,
    ProofOfDelivery = 7,
    CustomsDecision = 8,
    DangerousGoodsNote = 9,
    Other = 99
}

public class StoredDocument
{
    public Guid Id { get; set; }
    public int? ShipmentId { get; set; }
    public DocumentKind Kind { get; set; }
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "application/pdf";
    public long Length { get; set; }
    public string Sha256 { get; set; } = "";
    public bool VisibleToCustomer { get; set; }
    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? DeleteAfter { get; set; }
}
