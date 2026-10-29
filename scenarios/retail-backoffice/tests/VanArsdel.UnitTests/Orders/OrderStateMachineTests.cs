using FluentAssertions;
using MassTransit.Testing;
using VanArsdel.Contracts.Orders;
using VanArsdel.Orders.Api.Saga;
using Xunit;

namespace VanArsdel.UnitTests.Orders;

public class OrderStateMachineTests : IAsyncLifetime
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(5);
    private readonly InMemoryTestHarness _harness = new() { TestTimeout = TimeSpan.FromSeconds(10) };
    private readonly OrderStateMachine _machine = new();
    private StateMachineSagaTestHarness<OrderState, OrderStateMachine> _saga;

    public async Task InitializeAsync()
    {
        _saga = _harness.StateMachineSaga<OrderState, OrderStateMachine>(_machine);
        await _harness.Start();
    }

    public Task DisposeAsync() => _harness.Stop();

    private static OrderSubmitted Submitted(Guid id, bool paid, string delivery = DeliveryMethods.Home) => new()
    {
        OrderId = id,
        OrderNo = "W" + id.ToString("N").Substring(0, 6),
        Channel = "Web",
        Market = "FI",
        Currency = "EUR",
        DeliveryMethod = delivery,
        CollectStoreCode = delivery == DeliveryMethods.ClickAndCollect ? "FI012" : null,
        ShipToCountry = "FI",
        PaymentAuthorized = paid,
        Lines = { new OrderLineInfo { Sku = "VA1001-BLK-M", Quantity = 1, UnitPrice = 59.9m } },
    };

    private Task<Guid?> InState(Guid id, Func<OrderStateMachine, Automatonymous.State> state) =>
        _saga.Exists(id, state(_machine), Wait);

    private async Task Allocated(Guid id, int shipments)
    {
        await _harness.Bus.Publish(new OrderAllocated
        {
            OrderId = id,
            Shipments = Enumerable.Range(0, shipments).Select(_ => new AllocatedShipment { ShipmentId = Guid.NewGuid(), FromLocation = "DC01" }).ToList(),
        });
        (await InState(id, m => m.Fulfilling)).Should().NotBeNull();
    }

    [Fact]
    public async Task Unpaid_order_waits_for_the_payment()
    {
        var id = Guid.NewGuid();
        await _harness.Bus.Publish(Submitted(id, paid: false));

        (await InState(id, m => m.AwaitingPayment)).Should().NotBeNull();

        await _harness.Bus.Publish(new PaymentAuthorized { OrderId = id, Amount = 59.9m });

        (await InState(id, m => m.Allocating)).Should().NotBeNull();
        (await _harness.Published.Any<AllocateOrderRequested>(x => x.Context.Message.OrderId == id)).Should().BeTrue();
    }

    [Fact]
    public async Task Paid_order_goes_straight_to_allocation()
    {
        var id = Guid.NewGuid();
        await _harness.Bus.Publish(Submitted(id, paid: true));

        (await InState(id, m => m.Allocating)).Should().NotBeNull();
        (await _harness.Published.Any<AllocateOrder>(x => x.Context.Message.OrderId == id && x.Context.Message.Lines.Count == 1)).Should().BeTrue();
    }

    [Fact]
    public async Task Failed_payment_cancels_the_order()
    {
        var id = Guid.NewGuid();
        await _harness.Bus.Publish(Submitted(id, paid: false));
        (await InState(id, m => m.AwaitingPayment)).Should().NotBeNull();

        await _harness.Bus.Publish(new PaymentFailed { OrderId = id, Reason = "Declined" });

        (await InState(id, m => m.Cancelling)).Should().NotBeNull();
        (await _harness.Published.Any<CancelOrder>(x => x.Context.Message.OrderId == id && x.Context.Message.Reason.Contains("Declined"))).Should().BeTrue();
    }

    [Fact]
    public async Task Payment_is_captured_when_the_last_shipment_leaves_and_order_completes_on_delivery()
    {
        var id = Guid.NewGuid();
        await _harness.Bus.Publish(Submitted(id, paid: true));
        (await InState(id, m => m.Allocating)).Should().NotBeNull();
        await Allocated(id, 2);

        await _harness.Bus.Publish(new ShipmentDispatched { OrderId = id, ShipmentId = Guid.NewGuid() });
        await _harness.Bus.Publish(new ShipmentDispatched { OrderId = id, ShipmentId = Guid.NewGuid() });
        (await _harness.Published.Any<CapturePayment>(x => x.Context.Message.OrderId == id)).Should().BeTrue();

        await _harness.Bus.Publish(new ShipmentDelivered { OrderId = id });
        await _harness.Bus.Publish(new ShipmentDelivered { OrderId = id });

        (await InState(id, m => m.Completed)).Should().NotBeNull();
        (await _harness.Published.Any<CompleteOrder>(x => x.Context.Message.OrderId == id)).Should().BeTrue();
        _saga.Sagas.Contains(id).ShipmentsDispatched.Should().Be(2);
    }

    [Fact]
    public async Task Click_and_collect_completes_when_collected()
    {
        var id = Guid.NewGuid();
        await _harness.Bus.Publish(Submitted(id, paid: true, DeliveryMethods.ClickAndCollect));
        (await InState(id, m => m.Allocating)).Should().NotBeNull();
        await Allocated(id, 1);

        await _harness.Bus.Publish(new OrderReadyForCollection { OrderId = id, StoreCode = "FI012", CollectBy = DateTime.UtcNow.AddDays(7) });
        (await InState(id, m => m.AwaitingCollection)).Should().NotBeNull();

        await _harness.Bus.Publish(new OrderCollected { OrderId = id, StoreCode = "FI012" });

        (await InState(id, m => m.Completed)).Should().NotBeNull();
    }

    [Fact]
    public async Task Uncollected_order_is_cancelled_and_payment_released()
    {
        var id = Guid.NewGuid();
        await _harness.Bus.Publish(Submitted(id, paid: true, DeliveryMethods.ClickAndCollect));
        (await InState(id, m => m.Allocating)).Should().NotBeNull();
        await Allocated(id, 1);
        await _harness.Bus.Publish(new OrderReadyForCollection { OrderId = id, StoreCode = "FI012" });
        (await InState(id, m => m.AwaitingCollection)).Should().NotBeNull();

        await _harness.Bus.Publish(new CollectionExpired { OrderId = id, StoreCode = "FI012", CollectBy = new DateTime(2026, 11, 5) });

        (await InState(id, m => m.Cancelling)).Should().NotBeNull();
        (await _harness.Published.Any<CancelOrder>(x => x.Context.Message.OrderId == id && x.Context.Message.CancelPayment)).Should().BeTrue();

        await _harness.Bus.Publish(new OrderCancelled { OrderId = id });
        (await InState(id, m => m.CancelledState)).Should().NotBeNull();
    }

    [Fact]
    public async Task Out_of_stock_cancels_and_releases_the_payment()
    {
        var id = Guid.NewGuid();
        await _harness.Bus.Publish(Submitted(id, paid: true));
        (await InState(id, m => m.Allocating)).Should().NotBeNull();

        await _harness.Bus.Publish(new OrderAllocationFailed { OrderId = id, Reason = "Not enough stock" });

        (await InState(id, m => m.Cancelling)).Should().NotBeNull();
        (await _harness.Published.Any<CancelOrder>(x => x.Context.Message.OrderId == id && x.Context.Message.CancelPayment)).Should().BeTrue();
    }

    [Fact]
    public async Task Cancellation_for_an_unknown_order_is_discarded()
    {
        var id = Guid.NewGuid();
        await _harness.Bus.Publish(new OrderCancelled { OrderId = id });

        (await _harness.Consumed.Any<OrderCancelled>(x => x.Context.Message.OrderId == id)).Should().BeTrue();
        _saga.Sagas.Contains(id).Should().BeNull();
    }
}
