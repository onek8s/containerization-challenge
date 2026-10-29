using System;
using System.Runtime.Serialization;
using System.ServiceModel;

namespace Tailspin.DealerOrders.Infrastructure.DocumentService
{
    /// <summary>Quote and contract document service hosted at docs.corp.local (DocumentService.svc).</summary>
    [ServiceContract(Namespace = DocumentNamespaces.Service)]
    public interface IDocumentService
    {
        [OperationContract]
        [FaultContract(typeof(DocumentFault))]
        DocumentInfo GenerateQuote(int quoteId, string requestedBy);

        [OperationContract]
        [FaultContract(typeof(DocumentFault))]
        DocumentInfo GenerateOrderConfirmation(int orderId, string requestedBy);

        [OperationContract]
        [FaultContract(typeof(DocumentFault))]
        DocumentInfo GenerateDealerContract(int dealerId, int year, string requestedBy);

        [OperationContract]
        [FaultContract(typeof(DocumentFault))]
        DocumentInfo GeneratePriceList(int dealerId, string requestedBy);

        [OperationContract]
        [FaultContract(typeof(DocumentFault))]
        DocumentContent GetDocument(int documentId);

        [OperationContract]
        string Ping();
    }

    public static class DocumentNamespaces
    {
        public const string Service = "http://schemas.tailspin-machinery.example/dealerorders/documents/2016/04";
        public const string ClientKeyHeader = "ClientKey";
    }

    [DataContract(Namespace = DocumentNamespaces.Service)]
    public class DocumentInfo
    {
        [DataMember] public int Id { get; set; }
        [DataMember] public string FileName { get; set; }
        [DataMember] public string ContentType { get; set; }
        [DataMember] public int Size { get; set; }
        [DataMember] public string Sha256 { get; set; }
        [DataMember] public DateTime CreatedUtc { get; set; }
    }

    [DataContract(Namespace = DocumentNamespaces.Service)]
    public class DocumentContent
    {
        [DataMember] public int Id { get; set; }
        [DataMember] public int? DealerId { get; set; }
        [DataMember] public string FileName { get; set; }
        [DataMember] public string ContentType { get; set; }
        [DataMember] public byte[] Content { get; set; }
    }

    [DataContract(Namespace = DocumentNamespaces.Service)]
    public class DocumentFault
    {
        [DataMember] public string Code { get; set; }
        [DataMember] public string Message { get; set; }
    }
}
