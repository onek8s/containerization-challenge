using System;
using System.Runtime.Serialization;
using System.ServiceModel;

namespace VanArsdel.Integration.Contracts.Erp
{
    /// <summary>
    /// ERP order management service on erp01 (SOAP 1.1, basicHttp). BizTalk calls it through the WCF-Custom send port
    /// ERP.OrderManagement with the VanArsdel ERP header behaviour; the ops portal calls GetOrderStatus directly.
    /// </summary>
    [ServiceContract(Namespace = ErpNamespaces.Service, Name = "OrderManagementService")]
    public interface IOrderManagementService
    {
        [OperationContract(Action = ErpNamespaces.Service + "/OrderManagementService/CheckCredit")]
        [FaultContract(typeof(ErpFault))]
        CreditCheckResponse CheckCredit(CreditCheckRequest request);

        [OperationContract(Action = ErpNamespaces.Service + "/OrderManagementService/PriceOrder")]
        [FaultContract(typeof(ErpFault))]
        PriceOrderResponse PriceOrder(PriceOrderRequest request);

        [OperationContract(Action = ErpNamespaces.Service + "/OrderManagementService/GetOrderStatus")]
        [FaultContract(typeof(ErpFault))]
        OrderStatusResponse GetOrderStatus(OrderStatusRequest request);
    }

    public static class ErpNamespaces
    {
        public const string Service = "http://erp.vanarsdel.com/services/orders/2014";
        public const string Header = "http://erp.vanarsdel.com/services/header/2014";
    }

    [DataContract(Namespace = ErpNamespaces.Service)]
    public class CreditCheckRequest
    {
        [DataMember(Order = 1, IsRequired = true)]
        public string SoldTo { get; set; }

        [DataMember(Order = 2, IsRequired = true)]
        public decimal OrderValue { get; set; }

        [DataMember(Order = 3)]
        public string Currency { get; set; }

        [DataMember(Order = 4)]
        public string Reference { get; set; }
    }

    [DataContract(Namespace = ErpNamespaces.Service)]
    public class CreditCheckResponse
    {
        [DataMember(Order = 1)]
        public bool Approved { get; set; }

        [DataMember(Order = 2)]
        public decimal CreditLimit { get; set; }

        [DataMember(Order = 3)]
        public decimal OpenExposure { get; set; }

        [DataMember(Order = 4)]
        public string BlockReason { get; set; }
    }

    [DataContract(Namespace = ErpNamespaces.Service)]
    public class PriceOrderRequest
    {
        [DataMember(Order = 1, IsRequired = true)]
        public string SoldTo { get; set; }

        [DataMember(Order = 2)]
        public DateTime PricingDate { get; set; }

        [DataMember(Order = 3)]
        public PriceLine[] Lines { get; set; }
    }

    [DataContract(Namespace = ErpNamespaces.Service)]
    public class PriceLine
    {
        [DataMember(Order = 1)]
        public int LineNumber { get; set; }

        [DataMember(Order = 2)]
        public string Material { get; set; }

        [DataMember(Order = 3)]
        public decimal Quantity { get; set; }

        [DataMember(Order = 4)]
        public string Unit { get; set; }

        [DataMember(Order = 5)]
        public decimal NetPrice { get; set; }

        [DataMember(Order = 6)]
        public decimal PriceUnit { get; set; }

        [DataMember(Order = 7)]
        public bool Blocked { get; set; }
    }

    [DataContract(Namespace = ErpNamespaces.Service)]
    public class PriceOrderResponse
    {
        [DataMember(Order = 1)]
        public PriceLine[] Lines { get; set; }

        [DataMember(Order = 2)]
        public decimal NetValue { get; set; }

        [DataMember(Order = 3)]
        public string Currency { get; set; }
    }

    [DataContract(Namespace = ErpNamespaces.Service)]
    public class OrderStatusRequest
    {
        [DataMember(Order = 1)]
        public string SoldTo { get; set; }

        [DataMember(Order = 2)]
        public string CustomerReference { get; set; }

        [DataMember(Order = 3)]
        public string ErpOrderNumber { get; set; }
    }

    [DataContract(Namespace = ErpNamespaces.Service)]
    public class OrderStatusResponse
    {
        [DataMember(Order = 1)]
        public bool Found { get; set; }

        [DataMember(Order = 2)]
        public string ErpOrderNumber { get; set; }

        [DataMember(Order = 3)]
        public string Status { get; set; }

        [DataMember(Order = 4)]
        public DateTime? Created { get; set; }

        [DataMember(Order = 5)]
        public string DeliveryNumber { get; set; }

        [DataMember(Order = 6)]
        public string InvoiceNumber { get; set; }
    }

    [DataContract(Namespace = ErpNamespaces.Service)]
    public class ErpFault
    {
        [DataMember(Order = 1)]
        public string Code { get; set; }

        [DataMember(Order = 2)]
        public string Message { get; set; }
    }
}
