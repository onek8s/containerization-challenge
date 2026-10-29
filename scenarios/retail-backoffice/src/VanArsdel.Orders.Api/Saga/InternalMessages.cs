namespace VanArsdel.Orders.Api.Saga;

// Commands the saga sends to the order consumers in this service. Not part of the shared contracts.

public class CapturePayment
{
    public Guid OrderId { get; set; }
}

public class CompleteOrder
{
    public Guid OrderId { get; set; }
}

public class CancelOrder
{
    public Guid OrderId { get; set; }
    public string Reason { get; set; } = "";
    public bool CancelPayment { get; set; }
}
