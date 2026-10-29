namespace VanArsdel.BackOffice.Services;

// Copies of the service responses the screens use. Kept here on purpose: the back office must not
// reference the service projects.

public class Paged<T>
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int Total { get; set; }
    public List<T> Items { get; set; } = new();
}

public class ProductRow
{
    public Guid Id { get; set; }
    public string StyleCode { get; set; }
    public string Name { get; set; }
    public string Brand { get; set; }
    public string Category { get; set; }
    public string Season { get; set; }
    public string Status { get; set; }
    public decimal RecommendedPriceEur { get; set; }
    public int SkuCount { get; set; }
}

public class OrderRow
{
    public Guid Id { get; set; }
    public string OrderNo { get; set; }
    public string Channel { get; set; }
    public string Market { get; set; }
    public string Status { get; set; }
    public string CustomerEmail { get; set; }
    public string DeliveryMethod { get; set; }
    public string CollectStoreCode { get; set; }
    public decimal Total { get; set; }
    public string Currency { get; set; }
    public string PaymentStatus { get; set; }
    public DateTime PlacedUtc { get; set; }
    public int Shipments { get; set; }
}

public class OrderView
{
    public Guid Id { get; set; }
    public string OrderNo { get; set; }
    public string Channel { get; set; }
    public string Market { get; set; }
    public string Currency { get; set; }
    public string Status { get; set; }
    public string CustomerEmail { get; set; }
    public string CustomerFirstName { get; set; }
    public string CustomerLastName { get; set; }
    public string CustomerPhone { get; set; }
    public string LoyaltyCardNo { get; set; }
    public string DeliveryMethod { get; set; }
    public string CollectStoreCode { get; set; }
    public decimal ShippingFee { get; set; }
    public decimal Total { get; set; }
    public string PromotionCode { get; set; }
    public string PaymentReference { get; set; }
    public string PaymentMethod { get; set; }
    public string PaymentStatus { get; set; }
    public decimal CapturedAmount { get; set; }
    public DateTime PlacedUtc { get; set; }
    public string CancelReason { get; set; }
    public List<OrderLineRow> Lines { get; set; } = new();
    public List<ShipmentRow> Shipments { get; set; } = new();
    public List<OrderEventRow> Timeline { get; set; } = new();
    public SagaInfo Saga { get; set; }
}

public class OrderLineRow
{
    public int LineNo { get; set; }
    public string Sku { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Discount { get; set; }
}

public class ShipmentRow
{
    public Guid Id { get; set; }
    public string FromLocation { get; set; }
    public string Status { get; set; }
    public string Carrier { get; set; }
    public string TrackingNo { get; set; }
    public string TrackingUrl { get; set; }
    public int Parcels { get; set; }
}

public class OrderEventRow
{
    public string Type { get; set; }
    public string Text { get; set; }
    public string By { get; set; }
    public DateTime CreatedUtc { get; set; }
}

public class SagaInfo
{
    public string CurrentState { get; set; }
    public int ShipmentsTotal { get; set; }
    public int ShipmentsDispatched { get; set; }
    public int ShipmentsDelivered { get; set; }
}

public class CustomerRow
{
    public string Id { get; set; }
    public string Email { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Phone { get; set; }
    public string Market { get; set; }
    public string CardNo { get; set; }
    public string Tier { get; set; }
    public int Points { get; set; }
    public decimal RollingSpend { get; set; }
}

public class ReturnRow
{
    public string RmaNo { get; set; }
    public string OrderNo { get; set; }
    public string Channel { get; set; }
    public string StoreCode { get; set; }
    public string Status { get; set; }
    public string CustomerEmail { get; set; }
    public decimal RefundAmount { get; set; }
    public string Currency { get; set; }
    public DateTime CreatedUtc { get; set; }
}

public class StoreRow
{
    public string Code { get; set; }
    public string Name { get; set; }
    public string Type { get; set; }
    public string Country { get; set; }
    public string City { get; set; }
    public bool ShipFromStore { get; set; }
    public bool ClickAndCollect { get; set; }
    public DateTime OpenedOn { get; set; }
}

public class DayTotal
{
    public DateTime BusinessDate { get; set; }
    public string Channel { get; set; }
    public decimal NetEur { get; set; }
    public int Units { get; set; }
}

public class ReportInfo
{
    public string Name { get; set; }
    public string Title { get; set; }
}

public class ReportRun
{
    public long Id { get; set; }
    public string ReportName { get; set; }
    public DateTime BusinessDate { get; set; }
    public string FileName { get; set; }
    public int Rows { get; set; }
    public string RequestedBy { get; set; }
    public DateTime CreatedUtc { get; set; }
}

public class ReportReadyResult
{
    public string ReportName { get; set; }
    public string FileName { get; set; }
    public string DownloadUrl { get; set; }
}

public class ReturnDetail
{
    public string RmaNo { get; set; }
    public string Status { get; set; }
    public List<ReturnLineRow> Lines { get; set; } = new();
}

public class ReturnLineRow
{
    public string Sku { get; set; }
    public int Quantity { get; set; }
    public int ReceivedQuantity { get; set; }
}
