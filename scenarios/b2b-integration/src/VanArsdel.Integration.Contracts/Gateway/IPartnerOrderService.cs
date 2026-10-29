using System;
using System.Runtime.Serialization;
using System.ServiceModel;

namespace VanArsdel.Integration.Contracts.Gateway
{
    /// <summary>
    /// Partner gateway SOAP service at https://partners.corp.local/PartnerOrderService.svc (published to the internet
    /// through the reverse proxy as https://b2b.vanarsdel.com/partners/). WS-Security UserName over TLS; the user name
    /// is the PartnerId.
    /// </summary>
    [ServiceContract(Namespace = GatewayNamespaces.Service, Name = "PartnerOrderService")]
    [XmlSerializerFormat(SupportFaults = true)]
    public interface IPartnerOrderService
    {
        [OperationContract(Action = GatewayNamespaces.Service + "/SubmitOrders", ReplyAction = GatewayNamespaces.Service + "/SubmitOrdersResponse")]
        [FaultContract(typeof(GatewayFault))]
        SubmitReceipt SubmitOrders(OrderBatch batch);

        [OperationContract(Action = GatewayNamespaces.Service + "/GetOrderStatus", ReplyAction = GatewayNamespaces.Service + "/GetOrderStatusResponse")]
        [FaultContract(typeof(GatewayFault))]
        PartnerOrderStatus[] GetOrderStatus(string[] poNumbers);
    }

    public static class GatewayNamespaces
    {
        public const string Service = "http://schemas.vanarsdel.com/partners/services/2016";
    }

    [Serializable]
    [System.Xml.Serialization.XmlType(Namespace = GatewayNamespaces.Service)]
    public class SubmitReceipt
    {
        public string BatchId { get; set; }
        public string ReceiptId { get; set; }
        public int AcceptedOrders { get; set; }
        public DateTime ReceivedUtc { get; set; }

        [System.Xml.Serialization.XmlArrayItem("Warning")]
        public string[] Warnings { get; set; }
    }

    [Serializable]
    [System.Xml.Serialization.XmlType(Namespace = GatewayNamespaces.Service)]
    public class PartnerOrderStatus
    {
        public string PoNumber { get; set; }
        public string Status { get; set; }
        public string ErpOrderNumber { get; set; }
        public DateTime? ReceivedUtc { get; set; }
        public DateTime? DespatchedUtc { get; set; }
        public string TrackingNumber { get; set; }
        public string InvoiceNumber { get; set; }
    }

    [Serializable]
    [DataContract(Namespace = GatewayNamespaces.Service)]
    [System.Xml.Serialization.XmlType(Namespace = GatewayNamespaces.Service)]
    public class GatewayFault
    {
        [DataMember]
        public string Code { get; set; }

        [DataMember]
        public string Message { get; set; }
    }
}
