namespace FreightRates.Data.Entities;

public class Customer
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string ApiKeyHash { get; set; } = "";
    public decimal DiscountPercent { get; set; }
    public decimal MinimumCharge { get; set; }
    public decimal? FuelSurchargeOverridePercent { get; set; }
    public bool Active { get; set; } = true;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ModifiedUtc { get; set; }
    public string? ModifiedBy { get; set; }
}

public class Accessorial
{
    public string Code { get; set; } = "";
    public string Description { get; set; } = "";
    public decimal Amount { get; set; }
    public decimal? PerCwt { get; set; }
    public bool Active { get; set; } = true;
}

public class FuelPrice
{
    public int Id { get; set; }
    public DateOnly WeekStarting { get; set; }
    public decimal DieselPrice { get; set; }
}

public class FuelSurchargeStep
{
    public int Id { get; set; }
    public decimal DieselPriceFrom { get; set; }
    public decimal Percent { get; set; }
}

public class Quote
{
    public int Id { get; set; }
    public string Number { get; set; } = "";
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public string? CustomerReference { get; set; }
    public string OriginZip { get; set; } = "";
    public string DestinationZip { get; set; } = "";
    public DateOnly ShipDate { get; set; }
    public int TotalWeightLbs { get; set; }
    public string LinesJson { get; set; } = "[]";
    public string AccessorialCodes { get; set; } = "";
    public decimal GrossCharge { get; set; }
    public decimal Discount { get; set; }
    public decimal NetLinehaul { get; set; }
    public bool MinimumApplied { get; set; }
    public decimal FuelSurchargePercent { get; set; }
    public decimal FuelSurcharge { get; set; }
    public decimal AccessorialTotal { get; set; }
    public decimal Total { get; set; }
    public string TariffName { get; set; } = "";
    public int TransitDays { get; set; }
    public string OriginTerminal { get; set; } = "";
    public string DestinationTerminal { get; set; } = "";
    public DateOnly ValidUntil { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
