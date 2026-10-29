using System;
using System.Collections.Generic;

namespace Wingtip.FieldSales.Contracts
{
    /// <summary>Confirmed order as fetched by the ERP order import job.</summary>
    public class ErpOrderDto
    {
        public string OrderNo { get; set; }
        public string RepReference { get; set; }
        public string CustomerNo { get; set; }
        public string RepCode { get; set; }
        public DateTime OrderDate { get; set; }
        public DateTime RequestedDelivery { get; set; }
        public decimal NetTotal { get; set; }
        public string Comment { get; set; }
        public List<ErpOrderLineDto> Lines { get; set; }
    }

    public class ErpOrderLineDto
    {
        public int LineNo { get; set; }
        public string ItemNo { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineDiscountPercent { get; set; }
        public decimal LineTotal { get; set; }
    }

    public class ErpAcknowledgement
    {
        public string OrderNo { get; set; }
        public string ErpOrderNo { get; set; }
    }

    /// <summary>Customer master data pushed by the ERP every night and when credit control blocks a customer.</summary>
    public class ErpCustomerDto
    {
        public string CustomerNo { get; set; }
        public string Name { get; set; }
        public string Street { get; set; }
        public string PostalCode { get; set; }
        public string City { get; set; }
        public string PriceGroup { get; set; }
        public bool CreditBlocked { get; set; }
    }

    public class ErpCustomerResult
    {
        public int Inserted { get; set; }
        public int Updated { get; set; }
    }

    public class StockDto
    {
        public string ItemNo { get; set; }
        public int Available { get; set; }
        public DateTime? NextReceipt { get; set; }
    }
}
