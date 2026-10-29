using System;
using System.Collections.Generic;

namespace Tailspin.DealerOrders.Contracts.Ax
{
    /// <summary>Company, language and user an AX call runs under.</summary>
    public class AxCallContext
    {
        public string Company { get; set; } = "TSM";
        public string Language { get; set; } = "en-us";

        /// <summary>AX user alias to act as (Business Connector LogonAs / AIF CallContext.LogonAsUser). Empty: service identity.</summary>
        public string RunAsUser { get; set; }

        /// <summary>Message id for AIF idempotency (AifMessageLog) and log correlation.</summary>
        public string MessageId { get; set; } = Guid.NewGuid().ToString("D");

        public override string ToString() => $"{Company}/{Language}/{(string.IsNullOrEmpty(RunAsUser) ? "(service)" : RunAsUser)}";
    }

    public enum AxCustomerBlocked
    {
        No = 0,
        Invoice = 1,
        All = 2
    }

    public class AxCustomer
    {
        public string AccountNum { get; set; }
        public string Name { get; set; }
        public string CustGroup { get; set; }
        public string Currency { get; set; }
        public string LanguageId { get; set; }
        public string PriceGroup { get; set; }
        public string LineDiscount { get; set; }
        public string PaymTermId { get; set; }
        public string DlvTerm { get; set; }
        public string InventLocation { get; set; }
        public decimal CreditMax { get; set; }
        public string SalesDistrictId { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public AxCustomerBlocked Blocked { get; set; }
        public List<AxAddress> Addresses { get; set; } = new List<AxAddress>();
    }

    public class AxAddress
    {
        /// <summary>LogisticsLocation.LocationId; empty for new addresses.</summary>
        public string LocationId { get; set; }

        public string Description { get; set; }
        public string Street { get; set; }
        public string ZipCode { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string CountryRegionId { get; set; }
        public bool IsPrimaryDelivery { get; set; }
        public string ExternalCode { get; set; }
    }

    public class AxCustomerResult
    {
        public string AccountNum { get; set; }
        public bool Created { get; set; }
        public Dictionary<string, string> LocationIdsByExternalCode { get; set; } = new Dictionary<string, string>();
        public List<string> Infolog { get; set; } = new List<string>();
    }

    /// <summary>Header and lines of a sales quotation or sales order to create in AX.</summary>
    public class AxSalesDocument
    {
        public string CustAccount { get; set; }

        /// <summary>SalesTable.CustomerRef: the dealer's purchase order number.</summary>
        public string CustomerRef { get; set; }

        /// <summary>SalesTable.PurchOrderFormNum: the portal order or quote number.</summary>
        public string PortalReference { get; set; }

        public string CurrencyCode { get; set; }
        public DateTime? DeliveryDate { get; set; }
        public string DlvTerm { get; set; }
        public string DeliveryLocationId { get; set; }
        public string InventLocationId { get; set; }

        /// <summary>SalesTable.SalesOriginId: PORTAL, EDI or DESK.</summary>
        public string SalesOrigin { get; set; }

        public string EndCustomerName { get; set; }
        public decimal FreightCharge { get; set; }
        public string Notes { get; set; }
        public List<AxSalesLineData> Lines { get; set; } = new List<AxSalesLineData>();
    }

    public class AxSalesLineData
    {
        public int LineNumber { get; set; }
        public string ItemId { get; set; }
        public string ConfigId { get; set; }

        /// <summary>Option item ids that make up the configuration (BOM lines of the predefined variant).</summary>
        public List<string> OptionItemIds { get; set; } = new List<string>();

        public string Name { get; set; }
        public decimal Quantity { get; set; }
        public decimal SalesPrice { get; set; }
        public decimal LinePercent { get; set; }
        public DateTime? DeliveryDate { get; set; }
        public string ExternalItemId { get; set; }

        /// <summary>InventTransId of an existing line when updating; empty for new lines.</summary>
        public string InventTransId { get; set; }

        public bool Delete { get; set; }
    }

    public class AxDocumentResult
    {
        public string DocumentId { get; set; }
        public Dictionary<int, string> InventTransIdsByLine { get; set; } = new Dictionary<int, string>();
        public List<string> Infolog { get; set; } = new List<string>();
    }

    public class AxReservationResult
    {
        public string InventTransId { get; set; }
        public string ItemId { get; set; }
        public decimal Requested { get; set; }
        public decimal Reserved { get; set; }
        public DateTime? ExpectedDate { get; set; }
    }

    /// <summary>AX SalesStatus enum.</summary>
    public enum AxSalesStatus
    {
        None = 0,
        Backorder = 1,
        Delivered = 2,
        Invoiced = 3,
        Canceled = 4
    }

    /// <summary>AX DocumentStatus enum (last document posted for the order).</summary>
    public enum AxDocumentStatus
    {
        None = 0,
        Quotation = 1,
        PurchaseOrder = 2,
        Confirmation = 3,
        PickingList = 4,
        PackingSlip = 5,
        ReceiptsList = 6,
        Invoice = 7
    }

    public class AxSalesOrderSnapshot
    {
        public string SalesId { get; set; }
        public string CustAccount { get; set; }
        public string CustomerRef { get; set; }
        public AxSalesStatus SalesStatus { get; set; }
        public AxDocumentStatus DocumentStatus { get; set; }
        public DateTime? ConfirmedDeliveryDate { get; set; }

        /// <summary>The order is in production (production order started for at least one line).</summary>
        public bool InProduction { get; set; }

        public DateTime ModifiedDateTime { get; set; }
        public List<AxSalesLineSnapshot> Lines { get; set; } = new List<AxSalesLineSnapshot>();
        public List<AxPackingSlip> PackingSlips { get; set; } = new List<AxPackingSlip>();
        public List<string> InvoiceIds { get; set; } = new List<string>();
    }

    public class AxSalesLineSnapshot
    {
        public string InventTransId { get; set; }
        public string ItemId { get; set; }
        public string ConfigId { get; set; }
        public decimal QtyOrdered { get; set; }
        public decimal ReservedPhysical { get; set; }
        public decimal DeliveredQty { get; set; }
        public decimal InvoicedQty { get; set; }
        public DateTime? ConfirmedShipDate { get; set; }
        public string SerialId { get; set; }
        public AxSalesStatus LineStatus { get; set; }
    }

    public class AxPackingSlip
    {
        public string PackingSlipId { get; set; }
        public DateTime DeliveryDate { get; set; }
        public string CarrierCode { get; set; }
        public string TrackingNumber { get; set; }
        public string TrailerNumber { get; set; }
        public decimal GrossWeightKg { get; set; }
        public int Packages { get; set; }
        public List<AxPackingSlipLine> Lines { get; set; } = new List<AxPackingSlipLine>();
    }

    public class AxPackingSlipLine
    {
        public string InventTransId { get; set; }
        public string ItemId { get; set; }
        public decimal Quantity { get; set; }
        public string SerialId { get; set; }
    }

    public class AxPriceRecord
    {
        public string ItemRelation { get; set; }
        public string AccountRelation { get; set; }
        public string Currency { get; set; }
        public decimal Amount { get; set; }
        public decimal QuantityAmountFrom { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime? ToDate { get; set; }
    }

    public class AxStockRecord
    {
        public string ItemId { get; set; }
        public string ConfigId { get; set; }
        public string InventLocationId { get; set; }
        public decimal PhysicalInvent { get; set; }
        public decimal ReservPhysical { get; set; }
        public decimal OnOrder { get; set; }
        public DateTime? NextReceiptDate { get; set; }
    }

    public class AxCustomerBalance
    {
        public string AccountNum { get; set; }
        public decimal Balance { get; set; }
        public decimal CreditMax { get; set; }
        public AxCustomerBlocked Blocked { get; set; }
        public decimal OpenOrderValue { get; set; }
    }

    public class AxOrderHeaderStatus
    {
        public string SalesId { get; set; }
        public string PortalReference { get; set; }
        public AxSalesStatus SalesStatus { get; set; }
        public AxDocumentStatus DocumentStatus { get; set; }
        public DateTime ModifiedDateTime { get; set; }
    }

    public class AxExchangeRateRecord
    {
        public string FromCurrency { get; set; }
        public string ToCurrency { get; set; }
        public decimal Rate { get; set; }
        public DateTime ValidFrom { get; set; }
    }

    public class AxItemRecord
    {
        public string ItemId { get; set; }
        public string Name { get; set; }
        public string ItemGroup { get; set; }
        public decimal NetWeightKg { get; set; }
        public bool Stopped { get; set; }
        public string HarmonizedCode { get; set; }
    }

    public class AxPage<T>
    {
        public List<T> Records { get; set; } = new List<T>();

        /// <summary>Position for the next page (PositionBasedPaging.StartingPosition); null when no more data.</summary>
        public int? NextPosition { get; set; }
    }
}
