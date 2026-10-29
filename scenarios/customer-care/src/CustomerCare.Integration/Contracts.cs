using System;
using System.Runtime.Serialization;
using System.ServiceModel;

namespace Tailspin.CustomerCare.Integration
{
    public static class Namespaces
    {
        public const string Service = "urn:tailspin-energy:customercare:integration:2019";
        public const string Data = "urn:tailspin-energy:customercare:data:2019";
    }

    /// <summary>
    /// Case interface for the billing system, the outage management system and partner contact centres.
    /// </summary>
    [ServiceContract(Namespace = Namespaces.Service, Name = "CaseIntegration")]
    public interface ICaseIntegrationService
    {
        [OperationContract]
        [FaultContract(typeof(IntegrationFault))]
        CreateCaseResponse CreateCase(CreateCaseRequest request);

        [OperationContract]
        [FaultContract(typeof(IntegrationFault))]
        CaseInfo GetCase(string caseNumber);

        [OperationContract]
        [FaultContract(typeof(IntegrationFault))]
        CaseInfo[] FindByExternalReference(string externalReference);

        [OperationContract]
        [FaultContract(typeof(IntegrationFault))]
        CaseInfo[] GetOpenCasesForAccount(string accountNumber);

        [OperationContract]
        [FaultContract(typeof(IntegrationFault))]
        void AddCustomerMessage(string caseNumber, string text, string source);
    }

    [DataContract(Namespace = Namespaces.Data)]
    public class CreateCaseRequest
    {
        [DataMember(IsRequired = true, Order = 1)]
        public string AccountNumber { get; set; }

        [DataMember(IsRequired = true, Order = 2)]
        public string CategoryCode { get; set; }

        [DataMember(IsRequired = true, Order = 3)]
        public string Subject { get; set; }

        [DataMember(Order = 4)]
        public string Description { get; set; }

        /// <summary>1 = Low, 2 = Normal, 3 = High, 4 = Critical. Defaults to Normal.</summary>
        [DataMember(Order = 5)]
        public int? Priority { get; set; }

        /// <summary>Reference in the calling system, for example the billing dispute number.</summary>
        [DataMember(Order = 6)]
        public string ExternalReference { get; set; }

        /// <summary>When true and an open case with the same external reference exists, that case is returned.</summary>
        [DataMember(Order = 7)]
        public bool ReuseOpenCase { get; set; }
    }

    [DataContract(Namespace = Namespaces.Data)]
    public class CreateCaseResponse
    {
        [DataMember(Order = 1)]
        public string CaseNumber { get; set; }

        [DataMember(Order = 2)]
        public string QueueCode { get; set; }

        [DataMember(Order = 3)]
        public DateTime ResponseDueUtc { get; set; }

        [DataMember(Order = 4)]
        public DateTime ResolveDueUtc { get; set; }

        [DataMember(Order = 5)]
        public bool Existing { get; set; }
    }

    [DataContract(Namespace = Namespaces.Data)]
    public class CaseInfo
    {
        [DataMember(Order = 1)]
        public string CaseNumber { get; set; }

        [DataMember(Order = 2)]
        public string AccountNumber { get; set; }

        [DataMember(Order = 3)]
        public string Subject { get; set; }

        [DataMember(Order = 4)]
        public string Status { get; set; }

        [DataMember(Order = 5)]
        public string QueueCode { get; set; }

        [DataMember(Order = 6)]
        public int Priority { get; set; }

        [DataMember(Order = 7)]
        public string ExternalReference { get; set; }

        [DataMember(Order = 8)]
        public DateTime CreatedUtc { get; set; }

        [DataMember(Order = 9)]
        public DateTime ResolveDueUtc { get; set; }

        [DataMember(Order = 10)]
        public DateTime? ResolvedUtc { get; set; }

        [DataMember(Order = 11)]
        public string SlaState { get; set; }
    }

    [DataContract(Namespace = Namespaces.Data)]
    public class IntegrationFault
    {
        [DataMember(Order = 1)]
        public string Code { get; set; }

        [DataMember(Order = 2)]
        public string Message { get; set; }
    }
}
