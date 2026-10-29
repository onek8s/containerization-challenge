using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Tailspin.DealerOrders.Domain;

namespace Tailspin.DealerOrders.DealerPortal.Models
{
    public class QuoteViewModel
    {
        public int Id { get; set; }
        public string QuoteNumber { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime ValidUntil { get; set; }
        public QuoteStatus Status { get; set; }
        public string StatusText { get; set; }
        public string Currency { get; set; }
        public string EndCustomerName { get; set; }
        public string EndCustomerCountry { get; set; }
        public string Reference { get; set; }
        public decimal RequestedExtraDiscount { get; set; }
        public decimal ApprovedExtraDiscount { get; set; }
        public string ApprovalComment { get; set; }
        public string AxQuotationId { get; set; }
        public int? OrderId { get; set; }
        public string Notes { get; set; }
        public decimal TotalNet { get; set; }
        public decimal TotalList { get; set; }
        public bool CanEdit { get; set; }
        public bool CanAccept { get; set; }
        public List<QuoteLineViewModel> Lines { get; set; }
        public IList<DealerAddress> DeliveryAddresses { get; set; }
    }

    public class QuoteLineViewModel
    {
        public int LineNumber { get; set; }
        public string ItemId { get; set; }
        public string ModelCode { get; set; }
        public string ConfigurationId { get; set; }
        public string Description { get; set; }
        public List<string> OptionList { get; set; }
        public decimal Quantity { get; set; }
        public decimal ListPrice { get; set; }
        public decimal DiscountPercent { get; set; }
        public decimal NetPrice { get; set; }
        public decimal NetAmount { get; set; }
        public int LeadTimeWeeks { get; set; }
    }

    public class OrderViewModel
    {
        public int Id { get; set; }
        public string OrderNumber { get; set; }
        public OrderSource Source { get; set; }
        public OrderType OrderType { get; set; }
        public OrderStatus Status { get; set; }
        public string StatusText { get; set; }
        public string DealerPoNumber { get; set; }
        public string Currency { get; set; }
        public DateTime CreatedUtc { get; set; }
        public string CreatedBy { get; set; }
        public DateTime? RequestedDeliveryDate { get; set; }
        public DateTime? ConfirmedDeliveryDate { get; set; }
        public string DeliveryAddressText { get; set; }
        public DeliveryTerms DeliveryTerms { get; set; }
        public string EndCustomerName { get; set; }
        public int? QuoteId { get; set; }
        public decimal ExtraDiscount { get; set; }
        public decimal FreightCharge { get; set; }
        public string AxSalesId { get; set; }
        public string Notes { get; set; }
        public decimal TotalNet { get; set; }
        public decimal OrderValue { get; set; }
        public bool CanCancel { get; set; }
        public bool CanChange { get; set; }
        public bool HasEdi { get; set; }
        public List<OrderLineViewModel> Lines { get; set; }
        public List<StatusChangeViewModel> History { get; set; }
        public IList<StoredDocument> Documents { get; set; }
    }

    public class OrderLineViewModel
    {
        public int LineNumber { get; set; }
        public string ItemId { get; set; }
        public string ModelCode { get; set; }
        public string ConfigurationId { get; set; }
        public string Description { get; set; }
        public decimal Quantity { get; set; }
        public decimal ListPrice { get; set; }
        public decimal DiscountPercent { get; set; }
        public decimal NetPrice { get; set; }
        public decimal NetAmount { get; set; }
        public string DealerLineReference { get; set; }
        public ReservationStatus Reservation { get; set; }
        public decimal ShippedQuantity { get; set; }
        public DateTime? ConfirmedShipDate { get; set; }
        public string SerialNumber { get; set; }
        public bool Cancelled { get; set; }
    }

    public class StatusChangeViewModel
    {
        public string Status { get; set; }
        public string ChangedBy { get; set; }
        public DateTime ChangedUtc { get; set; }
        public string Comment { get; set; }
    }

    public class OrderRowViewModel
    {
        public int Id { get; set; }
        public string OrderNumber { get; set; }
        public string DealerPoNumber { get; set; }
        public OrderStatus Status { get; set; }
        public string StatusText { get; set; }
        public OrderSource Source { get; set; }
        public string Currency { get; set; }
        public decimal NetValue { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime? RequestedDeliveryDate { get; set; }
        public DateTime? ConfirmedDeliveryDate { get; set; }
        public string FirstItem { get; set; }
    }

    public class OrderListViewModel
    {
        public string Text { get; set; }
        public bool OpenOnly { get; set; } = true;
        public OrderStatus? Status { get; set; }
        public int Page { get; set; } = 1;
        public int Pages { get; set; }
        public int Total { get; set; }
        public IList<OrderRowViewModel> Orders { get; set; }
    }

    public class AcceptQuoteModel
    {
        public int QuoteId { get; set; }

        [Required, StringLength(30), Display(Name = "Your purchase order number")]
        public string DealerPoNumber { get; set; }

        [DataType(DataType.Date), Display(Name = "Requested delivery")]
        public DateTime? RequestedDelivery { get; set; }

        [Display(Name = "Deliver to")]
        public int? DeliveryAddressId { get; set; }
    }

    public class NewQuoteModel
    {
        public string ModelCode { get; set; }
        public List<string> Options { get; set; } = new List<string>();
        public decimal Quantity { get; set; } = 1;
        public decimal ExtraDiscount { get; set; }
        public string DeliveryCountry { get; set; }
        public string EndCustomer { get; set; }
        public string EndCustomerCountry { get; set; }
        public string Reference { get; set; }
        public int? AddToQuoteId { get; set; }
    }

    public class PartsOrderModel
    {
        public string DealerPoNumber { get; set; }
        public DateTime? RequestedDelivery { get; set; }
        public int? DeliveryAddressId { get; set; }
        public string Notes { get; set; }
        public List<PartsOrderLine> Lines { get; set; } = new List<PartsOrderLine>();
    }

    public class PartsOrderLine
    {
        public string ItemId { get; set; }
        public decimal Quantity { get; set; }
        public string Reference { get; set; }
    }

    public class DashboardViewModel
    {
        public Dealer Dealer { get; set; }
        public decimal AvailableCredit { get; set; }
        public Data.Queries.DashboardFigures Figures { get; set; }
        public IList<OrderRowViewModel> RecentOrders { get; set; }
        public IList<QuoteViewModel> OpenQuotes { get; set; }
    }
}
