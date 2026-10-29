using System;
using System.Collections.Generic;
using System.Linq;

namespace Tailspin.DealerOrders.Domain
{
    public class SalesOrder
    {
        public SalesOrder()
        {
            Lines = new List<OrderLine>();
            History = new List<OrderStatusChange>();
        }

        public int Id { get; set; }

        /// <summary>Portal order number (WO-yyyy-nnnnnn), also sent to AX as the customer requisition.</summary>
        public string OrderNumber { get; set; }

        public int DealerId { get; set; }
        public virtual Dealer Dealer { get; set; }
        public OrderSource Source { get; set; }
        public OrderType OrderType { get; set; }
        public OrderStatus Status { get; set; }

        /// <summary>Dealer purchase order number (CustomerRef in AX, BEG03 in EDI).</summary>
        public string DealerPoNumber { get; set; }

        public string Currency { get; set; }
        public DateTime CreatedUtc { get; set; }
        public string CreatedBy { get; set; }
        public DateTime? RequestedDeliveryDate { get; set; }
        public DateTime? ConfirmedDeliveryDate { get; set; }
        public int? DeliveryAddressId { get; set; }
        public virtual DealerAddress DeliveryAddress { get; set; }
        public DeliveryTerms DeliveryTerms { get; set; }
        public string EndCustomerName { get; set; }
        public int? QuoteId { get; set; }
        public decimal ExtraDiscount { get; set; }
        public decimal FreightCharge { get; set; }

        /// <summary>AX sales order id (SalesTable.SalesId), set by the connector.</summary>
        public string AxSalesId { get; set; }

        public string AxStatusText { get; set; }
        public DateTime? AxLastReadUtc { get; set; }
        public string ErrorMessage { get; set; }
        public int? EdiMessageId { get; set; }
        public string Notes { get; set; }
        public byte[] RowVersion { get; set; }

        public virtual ICollection<OrderLine> Lines { get; set; }
        public virtual ICollection<OrderStatusChange> History { get; set; }

        public decimal TotalNet => Lines.Where(l => !l.Cancelled).Sum(l => l.NetAmount);
        public decimal OrderValue => TotalNet + FreightCharge;

        public bool IsOpen => Status != OrderStatus.Invoiced && Status != OrderStatus.Cancelled && Status != OrderStatus.Rejected;

        public bool CanCancel => Status == OrderStatus.Draft || Status == OrderStatus.Submitted || Status == OrderStatus.PendingApproval ||
                                 Status == OrderStatus.CreditHold || Status == OrderStatus.Approved || Status == OrderStatus.CreatedInAx ||
                                 Status == OrderStatus.Confirmed || Status == OrderStatus.Error;

        public bool CanChange => Status == OrderStatus.Draft || Status == OrderStatus.PendingApproval ||
                                 Status == OrderStatus.CreatedInAx || Status == OrderStatus.Confirmed;

        public void ChangeStatus(OrderStatus status, string by, string comment, DateTime utcNow)
        {
            if (Status == status && string.IsNullOrEmpty(comment)) return;
            OrderStatusTransitions.Ensure(Status, status);
            History.Add(new OrderStatusChange
            {
                OrderId = Id,
                FromStatus = Status,
                ToStatus = status,
                ChangedBy = by,
                ChangedUtc = utcNow,
                Comment = comment
            });
            Status = status;
        }
    }

    public class OrderLine
    {
        public int Id { get; set; }
        public int OrderId { get; set; }
        public int LineNumber { get; set; }
        public string ItemId { get; set; }
        public string ModelCode { get; set; }
        public string ConfigurationId { get; set; }
        public string OptionCodes { get; set; }
        public string Description { get; set; }
        public decimal Quantity { get; set; }
        public decimal ListPrice { get; set; }
        public decimal DiscountPercent { get; set; }
        public decimal NetPrice { get; set; }

        /// <summary>Dealer's own part number / line id from EDI (PO1 01 and the BP qualifier).</summary>
        public string DealerLineReference { get; set; }
        public string DealerItemNumber { get; set; }

        /// <summary>AX SalesLine.InventTransId once the line exists in AX.</summary>
        public string AxInventTransId { get; set; }

        public ReservationStatus Reservation { get; set; }
        public decimal ReservedQuantity { get; set; }
        public decimal ShippedQuantity { get; set; }
        public decimal InvoicedQuantity { get; set; }
        public DateTime? ConfirmedShipDate { get; set; }
        public string SerialNumber { get; set; }
        public bool Cancelled { get; set; }

        public decimal NetAmount => NetPrice * Quantity;
        public decimal OpenQuantity => Cancelled ? 0 : Quantity - ShippedQuantity;
    }

    public class OrderStatusChange
    {
        public int Id { get; set; }
        public int OrderId { get; set; }
        public OrderStatus FromStatus { get; set; }
        public OrderStatus ToStatus { get; set; }
        public string ChangedBy { get; set; }
        public DateTime ChangedUtc { get; set; }
        public string Comment { get; set; }
    }

    public static class OrderStatusTransitions
    {
        private static readonly Dictionary<OrderStatus, OrderStatus[]> Allowed = new Dictionary<OrderStatus, OrderStatus[]>
        {
            [OrderStatus.Draft] = new[] { OrderStatus.Submitted, OrderStatus.Cancelled },
            [OrderStatus.Submitted] = new[] { OrderStatus.PendingApproval, OrderStatus.CreditHold, OrderStatus.Approved, OrderStatus.Rejected, OrderStatus.Cancelled },
            [OrderStatus.PendingApproval] = new[] { OrderStatus.Approved, OrderStatus.Rejected, OrderStatus.CreditHold, OrderStatus.Cancelled },
            [OrderStatus.CreditHold] = new[] { OrderStatus.Approved, OrderStatus.Rejected, OrderStatus.Cancelled, OrderStatus.PendingApproval },
            [OrderStatus.Approved] = new[] { OrderStatus.SentToAx, OrderStatus.Cancelled },
            [OrderStatus.SentToAx] = new[] { OrderStatus.CreatedInAx, OrderStatus.Error, OrderStatus.Confirmed },
            [OrderStatus.CreatedInAx] = new[] { OrderStatus.Confirmed, OrderStatus.InProduction, OrderStatus.PartiallyShipped, OrderStatus.Shipped, OrderStatus.Cancelled, OrderStatus.SentToAx, OrderStatus.Error },
            [OrderStatus.Confirmed] = new[] { OrderStatus.InProduction, OrderStatus.PartiallyShipped, OrderStatus.Shipped, OrderStatus.Cancelled, OrderStatus.SentToAx, OrderStatus.Error, OrderStatus.Invoiced },
            [OrderStatus.InProduction] = new[] { OrderStatus.PartiallyShipped, OrderStatus.Shipped, OrderStatus.Confirmed },
            [OrderStatus.PartiallyShipped] = new[] { OrderStatus.Shipped, OrderStatus.Invoiced },
            [OrderStatus.Shipped] = new[] { OrderStatus.Invoiced },
            [OrderStatus.Invoiced] = new OrderStatus[0],
            [OrderStatus.Cancelled] = new OrderStatus[0],
            [OrderStatus.Rejected] = new OrderStatus[0],
            [OrderStatus.Error] = new[] { OrderStatus.SentToAx, OrderStatus.Approved, OrderStatus.Cancelled, OrderStatus.CreatedInAx }
        };

        public static bool CanMove(OrderStatus from, OrderStatus to) => from == to || Allowed[from].Contains(to);

        public static void Ensure(OrderStatus from, OrderStatus to)
        {
            if (!CanMove(from, to))
                throw new InvalidOperationException($"Order status cannot change from {from} to {to}.");
        }
    }
}
