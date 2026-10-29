// Proxy types for the AX 2012 R3 AIF document services used by Dealer Orders. Originally generated with
// "Add Service Reference" against the TSDealerServices integration port and trimmed to the members we read.
using System;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Xml.Serialization;

namespace Tailspin.DealerOrders.AxIntegration.Aif
{
    internal static class AifNamespaces
    {
        public const string Services = "http://schemas.microsoft.com/dynamics/2008/01/services";
        public const string DataContracts = "http://schemas.microsoft.com/dynamics/2010/01/datacontracts";
        public const string EntityKeyList = "http://schemas.microsoft.com/dynamics/2006/02/documents/EntityKeyList";
        public const string EntityKey = "http://schemas.microsoft.com/dynamics/2006/02/documents/EntityKey";
        public const string SalesOrder = "http://schemas.microsoft.com/dynamics/2008/01/documents/SalesOrder";
        public const string Customer = "http://schemas.microsoft.com/dynamics/2008/01/documents/Customer";
        public const string InventOnhand = "http://schemas.microsoft.com/dynamics/2008/01/documents/InventOnhand";
        public const string QueryCriteria = "http://schemas.microsoft.com/dynamics/2006/02/documents/QueryCriteria";
        public const string Fault = "http://schemas.microsoft.com/dynamics/2008/01/documents/Fault";
    }

    [DataContract(Name = "CallContext", Namespace = AifNamespaces.DataContracts)]
    public class CallContext
    {
        [DataMember(Order = 0)] public string Company { get; set; }
        [DataMember(Order = 1)] public string Language { get; set; }
        [DataMember(Order = 2)] public string LogonAsUser { get; set; }
        [DataMember(Order = 3)] public string MessageId { get; set; }
        [DataMember(Order = 4)] public string PartitionKey { get; set; }
    }

    [XmlType(Namespace = AifNamespaces.EntityKey)]
    public class EntityKey
    {
        [XmlArray(Order = 0)]
        [XmlArrayItem("KeyField", IsNullable = false)]
        public KeyField[] KeyData { get; set; }
    }

    [XmlType(Namespace = AifNamespaces.EntityKey)]
    public class KeyField
    {
        [XmlElement(Order = 0)] public string Field { get; set; }
        [XmlElement(Order = 1)] public string Value { get; set; }
    }

    [XmlType(Namespace = AifNamespaces.QueryCriteria)]
    public class QueryCriteria
    {
        [XmlElement("CriteriaElement", Order = 0)] public CriteriaElement[] CriteriaElement { get; set; }
    }

    [XmlType(Namespace = AifNamespaces.QueryCriteria)]
    public class CriteriaElement
    {
        [XmlElement(Order = 0)] public string DataSourceName { get; set; }
        [XmlElement(Order = 1)] public string FieldName { get; set; }
        [XmlElement(Order = 2)] public string Operator { get; set; }
        [XmlElement(Order = 3)] public string Value1 { get; set; }
        [XmlElement(Order = 4)] public string Value2 { get; set; }
    }

    [DataContract(Name = "AifFault", Namespace = AifNamespaces.Fault)]
    public class AifFault
    {
        [DataMember] public string CustomDetailXml { get; set; }
        [DataMember] public FaultMessageList[] FaultMessageListArray { get; set; }
    }

    [DataContract(Name = "FaultMessageList", Namespace = AifNamespaces.Fault)]
    public class FaultMessageList
    {
        [DataMember] public string Document { get; set; }
        [DataMember] public string DocumentOperation { get; set; }
        [DataMember] public FaultMessage[] FaultMessageArray { get; set; }
    }

    [DataContract(Name = "FaultMessage", Namespace = AifNamespaces.Fault)]
    public class FaultMessage
    {
        [DataMember] public string Code { get; set; }
        [DataMember] public string Message { get; set; }
    }

    // ---- SalesOrderService -------------------------------------------------------------------------------------

    [ServiceContract(Namespace = AifNamespaces.Services, ConfigurationName = "SalesOrderService")]
    public interface SalesOrderService
    {
        [OperationContract(Action = AifNamespaces.Services + "/SalesOrderService/read", ReplyAction = AifNamespaces.Services + "/SalesOrderService/readResponse")]
        [FaultContract(typeof(AifFault), Action = AifNamespaces.Services + "/SalesOrderService/readAifFaultFault", Name = "AifFault", Namespace = AifNamespaces.Fault)]
        [XmlSerializerFormat(SupportFaults = true)]
        SalesOrderServiceReadResponse read(SalesOrderServiceReadRequest request);

        [OperationContract(Action = AifNamespaces.Services + "/SalesOrderService/find", ReplyAction = AifNamespaces.Services + "/SalesOrderService/findResponse")]
        [FaultContract(typeof(AifFault), Action = AifNamespaces.Services + "/SalesOrderService/findAifFaultFault", Name = "AifFault", Namespace = AifNamespaces.Fault)]
        [XmlSerializerFormat(SupportFaults = true)]
        SalesOrderServiceFindResponse find(SalesOrderServiceFindRequest request);
    }

    [MessageContract(WrapperName = "SalesOrderServiceReadRequest", WrapperNamespace = AifNamespaces.Services, IsWrapped = true)]
    public class SalesOrderServiceReadRequest
    {
        [MessageHeader(Namespace = AifNamespaces.DataContracts)] public CallContext CallContext;

        [MessageBodyMember(Namespace = AifNamespaces.Services, Order = 0)]
        [XmlArray(Namespace = AifNamespaces.EntityKeyList)]
        [XmlArrayItem("EntityKey", Namespace = AifNamespaces.EntityKey, IsNullable = false)]
        public EntityKey[] EntityKeyList;
    }

    [MessageContract(WrapperName = "SalesOrderServiceReadResponse", WrapperNamespace = AifNamespaces.Services, IsWrapped = true)]
    public class SalesOrderServiceReadResponse
    {
        [MessageBodyMember(Namespace = AifNamespaces.Services, Order = 0)]
        [XmlElement(Namespace = AifNamespaces.SalesOrder)]
        public AxdSalesOrder SalesOrder;
    }

    [MessageContract(WrapperName = "SalesOrderServiceFindRequest", WrapperNamespace = AifNamespaces.Services, IsWrapped = true)]
    public class SalesOrderServiceFindRequest
    {
        [MessageHeader(Namespace = AifNamespaces.DataContracts)] public CallContext CallContext;

        [MessageBodyMember(Namespace = AifNamespaces.Services, Order = 0)]
        [XmlElement(Namespace = AifNamespaces.QueryCriteria)]
        public QueryCriteria QueryCriteria;
    }

    [MessageContract(WrapperName = "SalesOrderServiceFindResponse", WrapperNamespace = AifNamespaces.Services, IsWrapped = true)]
    public class SalesOrderServiceFindResponse
    {
        [MessageBodyMember(Namespace = AifNamespaces.Services, Order = 0)]
        [XmlElement(Namespace = AifNamespaces.SalesOrder)]
        public AxdSalesOrder SalesOrder;
    }

    [XmlType(Namespace = AifNamespaces.SalesOrder)]
    public class AxdSalesOrder
    {
        [XmlElement("SalesTable", Order = 0)] public AxdEntity_SalesTable[] SalesTable { get; set; }
    }

    public enum AxdEnum_SalesStatus { None, Backorder, Delivered, Invoiced, Canceled }

    public enum AxdEnum_DocumentStatus { None, Quotation, PurchaseOrder, Confirmation, PickingList, PackingSlip, ReceiptsList, Invoice, ApproveJournal, ProjectInvoice, ProjectPackingSlip, CRMQuotation }

    [XmlType(Namespace = AifNamespaces.SalesOrder)]
    public class AxdEntity_SalesTable
    {
        [XmlElement(Order = 0)] public string SalesId { get; set; }
        [XmlElement(Order = 1)] public string CustAccount { get; set; }
        [XmlElement(Order = 2)] public string CustomerRef { get; set; }
        [XmlElement(Order = 3)] public string PurchOrderFormNum { get; set; }
        [XmlElement(Order = 4)] public AxdEnum_SalesStatus SalesStatus { get; set; }
        [XmlElement(Order = 5)] public AxdEnum_DocumentStatus DocumentStatus { get; set; }
        [XmlElement(DataType = "date", IsNullable = true, Order = 6)] public DateTime? ShippingDateConfirmed { get; set; }
        [XmlElement(Order = 7)] public string InventLocationId { get; set; }
        [XmlElement(Order = 8)] public DateTime ModifiedDateTime { get; set; }
        [XmlElement(Order = 9)] public string TSInProduction { get; set; }
        [XmlElement("SalesLine", Order = 10)] public AxdEntity_SalesLine[] SalesLine { get; set; }
    }

    [XmlType(Namespace = AifNamespaces.SalesOrder)]
    public class AxdEntity_SalesLine
    {
        [XmlElement(Order = 0)] public string InventTransId { get; set; }
        [XmlElement(Order = 1)] public string ItemId { get; set; }
        [XmlElement(Order = 2)] public decimal SalesQty { get; set; }
        [XmlElement(Order = 3)] public decimal RemainSalesPhysical { get; set; }
        [XmlElement(Order = 4)] public decimal RemainSalesFinancial { get; set; }
        [XmlElement(Order = 5)] public AxdEnum_SalesStatus SalesStatus { get; set; }
        [XmlElement(DataType = "date", IsNullable = true, Order = 6)] public DateTime? ShippingDateConfirmed { get; set; }
        [XmlElement("InventDim", Order = 7)] public AxdEntity_InventDim[] InventDim { get; set; }
    }

    [XmlType(Namespace = AifNamespaces.SalesOrder)]
    public class AxdEntity_InventDim
    {
        [XmlElement(Order = 0)] public string configId { get; set; }
        [XmlElement(Order = 1)] public string InventLocationId { get; set; }
        [XmlElement(Order = 2)] public string inventSerialId { get; set; }
    }

    // ---- CustomerService ---------------------------------------------------------------------------------------

    [ServiceContract(Namespace = AifNamespaces.Services, ConfigurationName = "CustomerService")]
    public interface CustomerService
    {
        [OperationContract(Action = AifNamespaces.Services + "/CustomerService/read", ReplyAction = AifNamespaces.Services + "/CustomerService/readResponse")]
        [FaultContract(typeof(AifFault), Action = AifNamespaces.Services + "/CustomerService/readAifFaultFault", Name = "AifFault", Namespace = AifNamespaces.Fault)]
        [XmlSerializerFormat(SupportFaults = true)]
        CustomerServiceReadResponse read(CustomerServiceReadRequest request);
    }

    [MessageContract(WrapperName = "CustomerServiceReadRequest", WrapperNamespace = AifNamespaces.Services, IsWrapped = true)]
    public class CustomerServiceReadRequest
    {
        [MessageHeader(Namespace = AifNamespaces.DataContracts)] public CallContext CallContext;

        [MessageBodyMember(Namespace = AifNamespaces.Services, Order = 0)]
        [XmlArray(Namespace = AifNamespaces.EntityKeyList)]
        [XmlArrayItem("EntityKey", Namespace = AifNamespaces.EntityKey, IsNullable = false)]
        public EntityKey[] EntityKeyList;
    }

    [MessageContract(WrapperName = "CustomerServiceReadResponse", WrapperNamespace = AifNamespaces.Services, IsWrapped = true)]
    public class CustomerServiceReadResponse
    {
        [MessageBodyMember(Namespace = AifNamespaces.Services, Order = 0)]
        [XmlElement(Namespace = AifNamespaces.Customer)]
        public AxdCustomer Customer;
    }

    [XmlType(Namespace = AifNamespaces.Customer)]
    public class AxdCustomer
    {
        [XmlElement("CustTable", Order = 0)] public AxdEntity_CustTable[] CustTable { get; set; }
    }

    [XmlType(Namespace = AifNamespaces.Customer)]
    public class AxdEntity_CustTable
    {
        [XmlElement(Order = 0)] public string AccountNum { get; set; }
        [XmlElement(Order = 1)] public string CustGroup { get; set; }
        [XmlElement(Order = 2)] public string Currency { get; set; }
        [XmlElement(Order = 3)] public string PriceGroup { get; set; }
        [XmlElement(Order = 4)] public string LineDisc { get; set; }
        [XmlElement(Order = 5)] public string PaymTermId { get; set; }
        [XmlElement(Order = 6)] public string DlvTerm { get; set; }
        [XmlElement(Order = 7)] public string InventLocation { get; set; }
        [XmlElement(Order = 8)] public decimal CreditMax { get; set; }
        [XmlElement(Order = 9)] public string SalesDistrictId { get; set; }
        [XmlElement(Order = 10)] public string Blocked { get; set; }
        [XmlElement(Order = 11)] public string LanguageId { get; set; }
        [XmlElement("DirParty", Order = 12)] public AxdEntity_DirParty[] DirParty { get; set; }
    }

    [XmlType(Namespace = AifNamespaces.Customer)]
    public class AxdEntity_DirParty
    {
        [XmlElement(Order = 0)] public string Name { get; set; }
    }

    // ---- InventOnhandService -----------------------------------------------------------------------------------

    [ServiceContract(Namespace = AifNamespaces.Services, ConfigurationName = "InventOnhandService")]
    public interface InventOnhandService
    {
        [OperationContract(Action = AifNamespaces.Services + "/InventOnhandService/find", ReplyAction = AifNamespaces.Services + "/InventOnhandService/findResponse")]
        [FaultContract(typeof(AifFault), Action = AifNamespaces.Services + "/InventOnhandService/findAifFaultFault", Name = "AifFault", Namespace = AifNamespaces.Fault)]
        [XmlSerializerFormat(SupportFaults = true)]
        InventOnhandServiceFindResponse find(InventOnhandServiceFindRequest request);
    }

    [MessageContract(WrapperName = "InventOnhandServiceFindRequest", WrapperNamespace = AifNamespaces.Services, IsWrapped = true)]
    public class InventOnhandServiceFindRequest
    {
        [MessageHeader(Namespace = AifNamespaces.DataContracts)] public CallContext CallContext;

        [MessageBodyMember(Namespace = AifNamespaces.Services, Order = 0)]
        [XmlElement(Namespace = AifNamespaces.QueryCriteria)]
        public QueryCriteria QueryCriteria;
    }

    [MessageContract(WrapperName = "InventOnhandServiceFindResponse", WrapperNamespace = AifNamespaces.Services, IsWrapped = true)]
    public class InventOnhandServiceFindResponse
    {
        [MessageBodyMember(Namespace = AifNamespaces.Services, Order = 0)]
        [XmlElement(Namespace = AifNamespaces.InventOnhand)]
        public AxdInventOnhand InventOnhand;
    }

    [XmlType(Namespace = AifNamespaces.InventOnhand)]
    public class AxdInventOnhand
    {
        [XmlElement("InventSum", Order = 0)] public AxdEntity_InventSum[] InventSum { get; set; }
    }

    [XmlType(Namespace = AifNamespaces.InventOnhand)]
    public class AxdEntity_InventSum
    {
        [XmlElement(Order = 0)] public string ItemId { get; set; }
        [XmlElement(Order = 1)] public decimal PhysicalInvent { get; set; }
        [XmlElement(Order = 2)] public decimal ReservPhysical { get; set; }
        [XmlElement(Order = 3)] public decimal OnOrder { get; set; }
        [XmlElement("InventDim", Order = 4)] public AxdEntity_InventDimOnhand[] InventDim { get; set; }
    }

    [XmlType(Namespace = AifNamespaces.InventOnhand)]
    public class AxdEntity_InventDimOnhand
    {
        [XmlElement(Order = 0)] public string configId { get; set; }
        [XmlElement(Order = 1)] public string InventLocationId { get; set; }
    }
}
