using System;
using System.Collections.Generic;

namespace Litware.OrderHub.Contracts
{
    /// <summary>An order from any sales channel, mapped to the hub format by the intake side.</summary>
    public interface OrderSubmitted
    {
        Guid OrderId { get; }
        string Channel { get; }
        string ExternalRef { get; }
        DateTime SubmittedUtc { get; }
        OrderDocument Order { get; }
    }

    /// <summary>Operator request from the portal to validate a rejected order again.</summary>
    public interface ResubmitOrder
    {
        Guid OrderId { get; }
        string RequestedBy { get; }
    }

    public interface OrderAccepted
    {
        Guid OrderId { get; }
        string OrderNumber { get; }
        string Channel { get; }
    }

    public interface OrderRejected
    {
        Guid OrderId { get; }
        string Channel { get; }
        string ExternalRef { get; }
        string Reason { get; }
    }

    /// <summary>Operator request from the portal to send the warehouse order file again.</summary>
    public interface ResendToWarehouse
    {
        Guid OrderId { get; }
        string RequestedBy { get; }
    }

    public interface OrderShipped
    {
        Guid OrderId { get; }
        string OrderNumber { get; }
        string Channel { get; }
        string ExternalRef { get; }
        string Carrier { get; }
        string TrackingNumber { get; }
        DateTime ShippedUtc { get; }
        IReadOnlyList<ShippedLine> Lines { get; }
    }

    public interface OrderInvoiced
    {
        Guid OrderId { get; }
        string OrderNumber { get; }
        string InvoiceNumber { get; }
    }

    public class ShippedLine
    {
        public string Sku { get; set; }
        public int Quantity { get; set; }
    }
}
