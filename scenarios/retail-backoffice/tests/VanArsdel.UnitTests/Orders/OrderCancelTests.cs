using FluentAssertions;
using VanArsdel.BuildingBlocks.Exceptions;
using VanArsdel.Contracts.Orders;
using VanArsdel.Orders.Api.Domain;
using Xunit;

namespace VanArsdel.UnitTests.Orders;

public class OrderCancelTests
{
    private static Order OrderWith(string status, string delivery, params string[] shipmentStatuses) => new()
    {
        OrderNo = "W1",
        Status = status,
        DeliveryMethod = delivery,
        Shipments = shipmentStatuses.Select(s => new Shipment { Id = Guid.NewGuid(), Status = s }).ToList(),
    };

    [Fact]
    public void Confirmed_order_with_requested_shipments_can_be_cancelled()
    {
        var order = OrderWith(OrderStatus.Confirmed, DeliveryMethods.Home, ShipmentStatus.Requested);

        order.Cancel("Customer changed their mind", "cs");

        order.Status.Should().Be(OrderStatus.Cancelled);
        order.Shipments.Should().OnlyContain(s => s.Status == ShipmentStatus.Cancelled);
    }

    [Fact]
    public void Shipped_order_cannot_be_cancelled()
    {
        var order = OrderWith(OrderStatus.Shipped, DeliveryMethods.Home, ShipmentStatus.Dispatched);

        var act = () => order.Cancel("Too late", "cs");

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Uncollected_parcel_can_be_cancelled()
    {
        var order = OrderWith(OrderStatus.ReadyForCollection, DeliveryMethods.ClickAndCollect, ShipmentStatus.ReadyForCollection);

        order.Cancel("Not collected", "system");

        order.Status.Should().Be(OrderStatus.Cancelled);
    }

    [Fact]
    public void Collected_parcel_cannot_be_cancelled()
    {
        var order = OrderWith(OrderStatus.ReadyForCollection, DeliveryMethods.ClickAndCollect, ShipmentStatus.Collected);

        order.CanCancel.Should().BeFalse();
    }
}
