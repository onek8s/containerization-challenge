using Refit;

namespace VanArsdel.Returns.Api.Clients;

/// <summary>Subset of the orders API used to validate returns.</summary>
public interface IOrdersApi
{
    [Get("/api/orders/{orderNo}")]
    Task<OrderView> GetOrderAsync(string orderNo);
}

public class OrderView
{
    public Guid Id { get; set; }
    public string OrderNo { get; set; } = "";
    public string Channel { get; set; } = "";
    public string Market { get; set; } = "";
    public string Currency { get; set; } = "";
    public string Status { get; set; } = "";
    public string CustomerEmail { get; set; } = "";
    public string LoyaltyCardNo { get; set; }
    public string PaymentReference { get; set; }
    public string PaymentMethod { get; set; }
    public string PaymentStatus { get; set; }
    public DateTime PlacedUtc { get; set; }
    public OrderAddressView ShipTo { get; set; }
    public List<OrderLineView> Lines { get; set; } = new();
}

public class OrderAddressView
{
    public string ShipToName { get; set; }
    public string ShipToStreet { get; set; }
    public string ShipToPostalCode { get; set; }
    public string ShipToCity { get; set; }
    public string ShipToCountry { get; set; }
}

public class OrderLineView
{
    public string Sku { get; set; } = "";
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Discount { get; set; }
}
