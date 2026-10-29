using Automatonymous;

namespace VanArsdel.Orders.Api.Saga;

public class OrderState : SagaStateMachineInstance
{
    public Guid CorrelationId { get; set; }
    public string CurrentState { get; set; }
    public string OrderNo { get; set; } = "";
    public string DeliveryMethod { get; set; } = "";
    public bool PaymentAuthorized { get; set; }
    public bool PaymentCaptured { get; set; }
    public int ShipmentsTotal { get; set; }
    public int ShipmentsDispatched { get; set; }
    public int ShipmentsDelivered { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public byte[] RowVersion { get; set; }
}
