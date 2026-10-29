using Automatonymous;
using MassTransit;
using VanArsdel.Contracts.Orders;

namespace VanArsdel.Orders.Api.Saga;

/// <summary>
/// Order fulfilment: payment, allocation, shipments, completion. The saga only tracks progress;
/// the order record is changed by the consumers it triggers.
/// </summary>
public class OrderStateMachine : MassTransitStateMachine<OrderState>
{
    public OrderStateMachine()
    {
        InstanceState(x => x.CurrentState);

        Event(() => Submitted, x => x.CorrelateById(m => m.Message.OrderId));
        Event(() => PaymentAuthorized, x => x.CorrelateById(m => m.Message.OrderId));
        Event(() => PaymentFailed, x => x.CorrelateById(m => m.Message.OrderId));
        Event(() => Allocated, x => x.CorrelateById(m => m.Message.OrderId));
        Event(() => AllocationFailed, x => x.CorrelateById(m => m.Message.OrderId));
        Event(() => Dispatched, x => x.CorrelateById(m => m.Message.OrderId));
        Event(() => Delivered, x => x.CorrelateById(m => m.Message.OrderId));
        Event(() => ReadyForCollection, x => x.CorrelateById(m => m.Message.OrderId));
        Event(() => Collected, x => x.CorrelateById(m => m.Message.OrderId));
        Event(() => CollectionExpired, x => x.CorrelateById(m => m.Message.OrderId));
        Event(() => Cancelled, x =>
        {
            x.CorrelateById(m => m.Message.OrderId);
            x.OnMissingInstance(m => m.Discard());
        });

        Initially(
            When(Submitted)
                .Then(ctx =>
                {
                    ctx.Instance.OrderNo = ctx.Data.OrderNo;
                    ctx.Instance.DeliveryMethod = ctx.Data.DeliveryMethod;
                    ctx.Instance.PaymentAuthorized = ctx.Data.PaymentAuthorized;
                    ctx.Instance.CreatedUtc = DateTime.UtcNow;
                    ctx.Instance.UpdatedUtc = DateTime.UtcNow;
                })
                .IfElse(ctx => ctx.Data.PaymentAuthorized,
                    authorized => authorized
                        .PublishAsync(ctx => ctx.Init<AllocateOrder>(ToAllocate(ctx.Data)))
                        .TransitionTo(Allocating),
                    pending => pending.TransitionTo(AwaitingPayment)));

        During(AwaitingPayment,
            When(PaymentAuthorized)
                .Then(ctx => ctx.Instance.PaymentAuthorized = true)
                .PublishAsync(ctx => ctx.Init<AllocateOrderRequested>(new AllocateOrderRequested { OrderId = ctx.Data.OrderId }))
                .TransitionTo(Allocating),
            When(PaymentFailed)
                .PublishAsync(ctx => ctx.Init<CancelOrder>(new CancelOrder { OrderId = ctx.Data.OrderId, Reason = "Payment failed: " + ctx.Data.Reason }))
                .TransitionTo(Cancelling));

        During(Allocating,
            When(Allocated)
                .Then(ctx =>
                {
                    ctx.Instance.ShipmentsTotal = ctx.Data.Shipments.Count;
                    ctx.Instance.UpdatedUtc = DateTime.UtcNow;
                })
                .TransitionTo(Fulfilling),
            When(AllocationFailed)
                .PublishAsync(ctx => ctx.Init<CancelOrder>(new CancelOrder { OrderId = ctx.Data.OrderId, Reason = "Out of stock: " + ctx.Data.Reason, CancelPayment = true }))
                .TransitionTo(Cancelling));

        During(Fulfilling,
            When(Dispatched)
                .Then(ctx =>
                {
                    ctx.Instance.ShipmentsDispatched++;
                    ctx.Instance.UpdatedUtc = DateTime.UtcNow;
                })
                .If(ctx => !ctx.Instance.PaymentCaptured && ctx.Instance.ShipmentsDispatched >= ctx.Instance.ShipmentsTotal,
                    capture => capture
                        .Then(ctx => ctx.Instance.PaymentCaptured = true)
                        .PublishAsync(ctx => ctx.Init<CapturePayment>(new CapturePayment { OrderId = ctx.Instance.CorrelationId }))),
            When(Delivered)
                .Then(ctx =>
                {
                    ctx.Instance.ShipmentsDelivered++;
                    ctx.Instance.UpdatedUtc = DateTime.UtcNow;
                })
                .If(ctx => ctx.Instance.ShipmentsDelivered >= ctx.Instance.ShipmentsTotal && ctx.Instance.DeliveryMethod != DeliveryMethods.ClickAndCollect,
                    done => done
                        .PublishAsync(ctx => ctx.Init<CompleteOrder>(new CompleteOrder { OrderId = ctx.Instance.CorrelationId }))
                        .TransitionTo(Completed)),
            When(ReadyForCollection)
                .TransitionTo(AwaitingCollection),
            When(Cancelled)
                .TransitionTo(CancelledState));

        During(AwaitingCollection,
            Ignore(Dispatched),
            Ignore(Delivered),
            When(Collected)
                .PublishAsync(ctx => ctx.Init<CompleteOrder>(new CompleteOrder { OrderId = ctx.Instance.CorrelationId }))
                .TransitionTo(Completed),
            When(CollectionExpired)
                .PublishAsync(ctx => ctx.Init<CancelOrder>(new CancelOrder
                {
                    OrderId = ctx.Data.OrderId,
                    Reason = "Not collected from " + ctx.Data.StoreCode + " by " + ctx.Data.CollectBy.ToString("yyyy-MM-dd"),
                    CancelPayment = true,
                }))
                .TransitionTo(Cancelling));

        During(Cancelling,
            When(Cancelled)
                .TransitionTo(CancelledState));

        DuringAny(
            When(Cancelled)
                .TransitionTo(CancelledState));

        During(Completed, CancelledState,
            Ignore(Dispatched),
            Ignore(Delivered),
            Ignore(Collected),
            Ignore(CollectionExpired),
            Ignore(PaymentAuthorized));
    }

    public State AwaitingPayment { get; private set; }
    public State Allocating { get; private set; }
    public State Fulfilling { get; private set; }
    public State AwaitingCollection { get; private set; }
    public State Cancelling { get; private set; }
    public State Completed { get; private set; }
    public State CancelledState { get; private set; }

    public Event<OrderSubmitted> Submitted { get; private set; }
    public Event<PaymentAuthorized> PaymentAuthorized { get; private set; }
    public Event<PaymentFailed> PaymentFailed { get; private set; }
    public Event<OrderAllocated> Allocated { get; private set; }
    public Event<OrderAllocationFailed> AllocationFailed { get; private set; }
    public Event<ShipmentDispatched> Dispatched { get; private set; }
    public Event<ShipmentDelivered> Delivered { get; private set; }
    public Event<OrderReadyForCollection> ReadyForCollection { get; private set; }
    public Event<OrderCollected> Collected { get; private set; }
    public Event<CollectionExpired> CollectionExpired { get; private set; }
    public Event<OrderCancelled> Cancelled { get; private set; }

    private static AllocateOrder ToAllocate(OrderSubmitted s) => new()
    {
        OrderId = s.OrderId,
        OrderNo = s.OrderNo,
        DeliveryMethod = s.DeliveryMethod,
        ShipToCountry = s.ShipToCountry,
        CollectStoreCode = s.CollectStoreCode,
        Lines = s.Lines,
    };
}

/// <summary>Payment came later than the order: the consumer rebuilds AllocateOrder from the order record.</summary>
public class AllocateOrderRequested
{
    public Guid OrderId { get; set; }
}
