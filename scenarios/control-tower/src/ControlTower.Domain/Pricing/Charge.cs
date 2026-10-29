namespace Proseware.ControlTower.Domain.Pricing;

public class Charge
{
    public int Id { get; set; }
    public int ShipmentId { get; set; }
    public string Code { get; set; } = "";
    public string Description { get; set; } = "";
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "EUR";
    public bool Invoiced { get; set; }
    public string? InvoiceNumber { get; set; }
}

public static class ChargeCodes
{
    public const string Freight = "FRT";
    public const string Fuel = "BAF";
    public const string Pickup = "PUP";
    public const string Delivery = "DEL";
    public const string Customs = "CUS";
    public const string DangerousGoods = "ADR";
    public const string Reefer = "TMP";
    public const string Waiting = "WTG";
    public const string Insurance = "INS";
}
