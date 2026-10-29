using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using log4net;
using Tailspin.DealerOrders.Contracts.Ax;
using Tailspin.DealerOrders.Domain;
using Tailspin.DealerOrders.Processing.Edi;
using Tailspin.DealerOrders.Processing.Notifications;
using Tailspin.DealerOrders.Processing.Ports;

namespace Tailspin.DealerOrders.Processing.Ax
{
    /// <summary>
    /// Applies an AX sales order snapshot to the portal order: line quantities, ship dates and serial numbers, new
    /// packing slips (shipments) and the resulting order status. Raises dealer notifications and EDI 855/856 messages.
    /// </summary>
    public class OrderStatusSync
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(OrderStatusSync));

        private readonly IOrderStore _orders;
        private readonly IDealerStore _dealers;
        private readonly IShipmentStore _shipments;
        private readonly NotificationComposer _notifications;
        private readonly EdiOutbound _edi;
        private readonly IClock _clock;

        public OrderStatusSync(IOrderStore orders, IDealerStore dealers, IShipmentStore shipments, NotificationComposer notifications, EdiOutbound edi, IClock clock)
        {
            _orders = orders;
            _dealers = dealers;
            _shipments = shipments;
            _notifications = notifications;
            _edi = edi;
            _clock = clock;
        }

        public IList<string> Apply(SalesOrder order, AxSalesOrderSnapshot snapshot)
        {
            var changes = new List<string>();
            var dealer = _dealers.Get(order.DealerId);
            var now = _clock.UtcNow;

            foreach (var axLine in snapshot.Lines)
            {
                var line = order.Lines.FirstOrDefault(l => l.AxInventTransId == axLine.InventTransId)
                           ?? order.Lines.FirstOrDefault(l => l.AxInventTransId == null && l.ItemId == axLine.ItemId && (l.ConfigurationId ?? "") == (axLine.ConfigId ?? ""));
                if (line == null)
                {
                    Log.WarnFormat("AX order {0} has line {1} ({2}) that is not on portal order {3}", snapshot.SalesId, axLine.InventTransId, axLine.ItemId, order.OrderNumber);
                    continue;
                }
                line.AxInventTransId = axLine.InventTransId;
                line.ShippedQuantity = axLine.DeliveredQty;
                line.InvoicedQuantity = axLine.InvoicedQty;
                line.ReservedQuantity = axLine.ReservedPhysical;
                if (axLine.ReservedPhysical > 0)
                    line.Reservation = axLine.ReservedPhysical >= axLine.QtyOrdered ? ReservationStatus.Reserved : ReservationStatus.PartiallyReserved;
                if (axLine.ConfirmedShipDate.HasValue) line.ConfirmedShipDate = axLine.ConfirmedShipDate;
                if (!string.IsNullOrEmpty(axLine.SerialId)) line.SerialNumber = axLine.SerialId;
                if (axLine.LineStatus == AxSalesStatus.Canceled && !line.Cancelled)
                {
                    line.Cancelled = true;
                    changes.Add($"line {line.LineNumber} cancelled in AX");
                }
            }

            var previousDate = order.ConfirmedDeliveryDate;
            if (snapshot.ConfirmedDeliveryDate.HasValue && snapshot.DocumentStatus >= AxDocumentStatus.Confirmation)
                order.ConfirmedDeliveryDate = snapshot.ConfirmedDeliveryDate;

            var newShipments = new List<Shipment>();
            foreach (var slip in snapshot.PackingSlips)
            {
                if (_shipments.Find(order.Id, slip.PackingSlipId) != null) continue;
                var shipment = new Shipment
                {
                    OrderId = order.Id,
                    PackingSlipId = slip.PackingSlipId,
                    ShipDate = slip.DeliveryDate,
                    CarrierCode = slip.CarrierCode,
                    TrackingNumber = slip.TrackingNumber,
                    TrailerNumber = slip.TrailerNumber,
                    GrossWeightKg = slip.GrossWeightKg,
                    Packages = slip.Packages,
                    Lines = slip.Lines.Select(l => new ShipmentLine
                    {
                        OrderLineNumber = order.Lines.FirstOrDefault(o => o.AxInventTransId == l.InventTransId)?.LineNumber ?? 0,
                        ItemId = l.ItemId,
                        Quantity = l.Quantity,
                        SerialNumber = l.SerialId
                    }).ToList()
                };
                _shipments.Add(shipment);
                newShipments.Add(shipment);
                changes.Add("packing slip " + slip.PackingSlipId);
            }

            var target = TargetStatus(order, snapshot);
            var oldStatus = order.Status;
            if (target != order.Status)
            {
                if (OrderStatusTransitions.CanMove(order.Status, target))
                {
                    order.ChangeStatus(target, "ax", $"AX {snapshot.SalesStatus}/{snapshot.DocumentStatus}", now);
                    changes.Add($"{oldStatus} -> {target}");
                }
                else
                {
                    Log.WarnFormat("Order {0}: AX status {1}/{2} would move {3} to {4}, ignored", order.OrderNumber, snapshot.SalesStatus, snapshot.DocumentStatus, order.Status, target);
                }
            }

            order.AxStatusText = $"{snapshot.SalesStatus}, last document {snapshot.DocumentStatus}";
            order.AxLastReadUtc = now;
            _orders.Save(order);

            if (oldStatus != OrderStatus.Confirmed && order.Status == OrderStatus.Confirmed)
            {
                _notifications.OrderEvent(NotificationKind.OrderConfirmed, order, dealer);
                if (order.Source == OrderSource.Edi) _edi.QueueAcknowledgment(order, dealer);
            }
            else if (previousDate.HasValue && order.ConfirmedDeliveryDate.HasValue && previousDate.Value.Date != order.ConfirmedDeliveryDate.Value.Date)
            {
                _notifications.OrderEvent(NotificationKind.DeliveryDateChanged, order, dealer,
                    new Dictionary<string, string> { ["PreviousDate"] = previousDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) });
                changes.Add("delivery date " + order.ConfirmedDeliveryDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                if (order.Source == OrderSource.Edi) _edi.QueueAcknowledgment(order, dealer);
            }

            foreach (var shipment in newShipments)
            {
                _notifications.OrderEvent(NotificationKind.OrderShipped, order, dealer, new Dictionary<string, string>
                {
                    ["ShipDate"] = shipment.ShipDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    ["PackingSlip"] = shipment.PackingSlipId,
                    ["Carrier"] = shipment.CarrierCode ?? "-",
                    ["Tracking"] = shipment.TrackingNumber ?? "-"
                });
                if (order.Source == OrderSource.Edi) _edi.QueueShipNotice(order, dealer, shipment);
            }

            if (oldStatus != OrderStatus.Invoiced && order.Status == OrderStatus.Invoiced)
                _notifications.OrderEvent(NotificationKind.OrderInvoiced, order, dealer,
                    new Dictionary<string, string> { ["InvoiceIds"] = string.Join(", ", snapshot.InvoiceIds) });

            if (oldStatus != OrderStatus.Cancelled && order.Status == OrderStatus.Cancelled)
                _notifications.OrderEvent(NotificationKind.OrderRejected, order, dealer,
                    new Dictionary<string, string> { ["Reason"] = "The order was cancelled in AX." });

            return changes;
        }

        public static OrderStatus TargetStatus(SalesOrder order, AxSalesOrderSnapshot snapshot)
        {
            switch (snapshot.SalesStatus)
            {
                case AxSalesStatus.Canceled:
                    return OrderStatus.Cancelled;
                case AxSalesStatus.Invoiced:
                    return OrderStatus.Invoiced;
                case AxSalesStatus.Delivered:
                    return snapshot.DocumentStatus == AxDocumentStatus.Invoice ? OrderStatus.Invoiced : OrderStatus.Shipped;
            }

            if (snapshot.Lines.Any(l => l.DeliveredQty > 0)) return OrderStatus.PartiallyShipped;
            if (snapshot.InProduction) return OrderStatus.InProduction;
            if (snapshot.DocumentStatus >= AxDocumentStatus.Confirmation) return OrderStatus.Confirmed;
            return order.Status == OrderStatus.SentToAx || order.Status == OrderStatus.Error ? OrderStatus.CreatedInAx : order.Status;
        }
    }
}
