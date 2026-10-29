using System;
using System.Collections.Generic;

namespace Wingtip.FieldSales.Contracts
{
    public class OrderLineDto
    {
        public string ItemNo { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineDiscountPercent { get; set; }
    }

    public class OrderSubmitRequest
    {
        public Guid ClientOrderId { get; set; }
        public string RepReference { get; set; }
        public string CustomerNo { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime RequestedDelivery { get; set; }
        public long PriceListVersion { get; set; }
        public string Comment { get; set; }
        public List<OrderLineDto> Lines { get; set; }
    }

    public static class SubmitResults
    {
        public const string Accepted = "Accepted";
        public const string AlreadyReceived = "AlreadyReceived";
        public const string PriceChanged = "PriceChanged";
        public const string Invalid = "Invalid";
    }

    public class PriceDifferenceDto
    {
        public string ItemNo { get; set; }
        public decimal OrderedPrice { get; set; }
        public decimal CurrentPrice { get; set; }
    }

    public class OrderSubmitResponse
    {
        public string Result { get; set; }
        public string OrderNo { get; set; }
        public string Status { get; set; }
        public decimal NetTotal { get; set; }
        public List<string> Errors { get; set; }
        public List<PriceDifferenceDto> PriceDifferences { get; set; }
    }

    public class OrderSummaryDto
    {
        public Guid ClientOrderId { get; set; }
        public string OrderNo { get; set; }
        public string RepReference { get; set; }
        public string CustomerNo { get; set; }
        public string CustomerName { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime RequestedDelivery { get; set; }
        public string Status { get; set; }
        public decimal NetTotal { get; set; }
        public string DecisionComment { get; set; }
    }

    public class MeResponse
    {
        public string UserName { get; set; }
        public string DisplayName { get; set; }
        public string RepCode { get; set; }
        public decimal DiscountLimitPercent { get; set; }
        public List<string> Roles { get; set; }
        public string MinimumClientVersion { get; set; }
    }
}
