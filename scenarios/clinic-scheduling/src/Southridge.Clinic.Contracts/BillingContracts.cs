using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.ServiceModel;

namespace Southridge.Clinic.Contracts
{
    /// <summary>Billing office integration for the hospital billing office and the payment kiosk vendor (wsHttpBinding).</summary>
    [ServiceContract(Namespace = Namespaces.Billing, Name = "BillingService", ProtectionLevel = System.Net.Security.ProtectionLevel.EncryptAndSign)]
    public interface IBillingService
    {
        [OperationContract]
        [FaultContract(typeof(ClinicFault))]
        AccountSummary GetAccount(string mrn);

        [OperationContract]
        [FaultContract(typeof(ClinicFault))]
        List<ClaimInfo> GetClaims(string mrn, DateTime from, DateTime to);

        [OperationContract]
        [FaultContract(typeof(ClinicFault))]
        PaymentReceipt PostPayment(PatientPayment payment);

        [OperationContract]
        [FaultContract(typeof(ClinicFault))]
        byte[] GetStatementPdf(string mrn);
    }

    [DataContract(Namespace = Namespaces.Billing)]
    public class AccountSummary
    {
        [DataMember(Order = 0)] public string Mrn { get; set; }
        [DataMember(Order = 1)] public string Name { get; set; }
        [DataMember(Order = 2)] public decimal Balance { get; set; }
        [DataMember(Order = 3)] public DateTime? LastStatementDate { get; set; }
        [DataMember(Order = 4)] public decimal? LastStatementAmount { get; set; }
        [DataMember(Order = 5)] public int DunningLevel { get; set; }
    }

    [DataContract(Namespace = Namespaces.Billing)]
    public class ClaimInfo
    {
        [DataMember(Order = 0)] public string ClaimNumber { get; set; }
        [DataMember(Order = 1)] public DateTime DateOfService { get; set; }
        [DataMember(Order = 2)] public string PayerName { get; set; }
        [DataMember(Order = 3)] public string Status { get; set; }
        [DataMember(Order = 4)] public decimal Charged { get; set; }
        [DataMember(Order = 5)] public decimal Paid { get; set; }
        [DataMember(Order = 6)] public decimal Adjusted { get; set; }
        [DataMember(Order = 7)] public decimal PatientResponsibility { get; set; }
    }

    [DataContract(Namespace = Namespaces.Billing)]
    public class PatientPayment
    {
        [DataMember(Order = 0, IsRequired = true)] public string Mrn { get; set; }
        [DataMember(Order = 1, IsRequired = true)] public decimal Amount { get; set; }
        [DataMember(Order = 2, IsRequired = true)] public string Method { get; set; }
        [DataMember(Order = 3)] public string Reference { get; set; }
    }

    [DataContract(Namespace = Namespaces.Billing)]
    public class PaymentReceipt
    {
        [DataMember(Order = 0)] public string Mrn { get; set; }
        [DataMember(Order = 1)] public decimal Amount { get; set; }
        [DataMember(Order = 2)] public decimal NewBalance { get; set; }
        [DataMember(Order = 3)] public DateTime PostedOn { get; set; }
    }
}
