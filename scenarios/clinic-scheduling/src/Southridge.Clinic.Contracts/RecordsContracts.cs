using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.ServiceModel;

namespace Southridge.Clinic.Contracts
{
    /// <summary>
    /// Patient records for partner organisations (wsHttpBinding, message security with user name credentials).
    /// </summary>
    [ServiceContract(Namespace = Namespaces.Records, Name = "RecordsService", ProtectionLevel = System.Net.Security.ProtectionLevel.EncryptAndSign)]
    public interface IRecordsService
    {
        [OperationContract]
        [FaultContract(typeof(ClinicFault))]
        PatientRecord GetPatient(string mrn);

        [OperationContract]
        [FaultContract(typeof(ClinicFault))]
        List<PatientMatch> FindPatients(string lastName, DateTime dateOfBirth);

        [OperationContract]
        [FaultContract(typeof(ClinicFault))]
        List<VisitInfo> GetVisits(string mrn, DateTime from, DateTime to);

        [OperationContract]
        [FaultContract(typeof(ClinicFault))]
        byte[] GetVisitSummaryPdf(int encounterId);

        [OperationContract]
        [FaultContract(typeof(ClinicFault))]
        List<LabResultInfo> GetLabResults(string mrn, DateTime from);

        [OperationContract]
        [FaultContract(typeof(ClinicFault))]
        ReferralReceipt SubmitReferral(ReferralSubmission referral);

        [OperationContract]
        [FaultContract(typeof(ClinicFault))]
        ReferralStatusInfo GetReferralStatus(string referralNumber);
    }

    [DataContract(Namespace = Namespaces.Records)]
    public class PatientRecord
    {
        [DataMember(Order = 0)] public string Mrn { get; set; }
        [DataMember(Order = 1)] public string FirstName { get; set; }
        [DataMember(Order = 2)] public string MiddleName { get; set; }
        [DataMember(Order = 3)] public string LastName { get; set; }
        [DataMember(Order = 4)] public DateTime DateOfBirth { get; set; }
        [DataMember(Order = 5)] public string Sex { get; set; }
        [DataMember(Order = 6)] public string Address1 { get; set; }
        [DataMember(Order = 7)] public string City { get; set; }
        [DataMember(Order = 8)] public string State { get; set; }
        [DataMember(Order = 9)] public string Zip { get; set; }
        [DataMember(Order = 10)] public string Phone { get; set; }
        [DataMember(Order = 11)] public string PrimaryProvider { get; set; }
        [DataMember(Order = 12)] public List<CoverageInfo> Coverage { get; set; }
        [DataMember(Order = 13)] public string Allergies { get; set; }
    }

    [DataContract(Namespace = Namespaces.Records)]
    public class CoverageInfo
    {
        [DataMember(Order = 0)] public int Sequence { get; set; }
        [DataMember(Order = 1)] public string PayerName { get; set; }
        [DataMember(Order = 2)] public string MemberId { get; set; }
        [DataMember(Order = 3)] public string GroupNumber { get; set; }
        [DataMember(Order = 4)] public DateTime EffectiveFrom { get; set; }
        [DataMember(Order = 5)] public DateTime? EffectiveTo { get; set; }
    }

    [DataContract(Namespace = Namespaces.Records)]
    public class PatientMatch
    {
        [DataMember(Order = 0)] public string Mrn { get; set; }
        [DataMember(Order = 1)] public string Name { get; set; }
        [DataMember(Order = 2)] public DateTime DateOfBirth { get; set; }
        [DataMember(Order = 3)] public string City { get; set; }
    }

    [DataContract(Namespace = Namespaces.Records)]
    public class VisitInfo
    {
        [DataMember(Order = 0)] public int EncounterId { get; set; }
        [DataMember(Order = 1)] public DateTime DateOfService { get; set; }
        [DataMember(Order = 2)] public string ProviderName { get; set; }
        [DataMember(Order = 3)] public string SiteName { get; set; }
        [DataMember(Order = 4)] public List<string> Diagnoses { get; set; }
        [DataMember(Order = 5)] public List<string> Procedures { get; set; }
    }

    [DataContract(Namespace = Namespaces.Records)]
    public class LabResultInfo
    {
        [DataMember(Order = 0)] public string OrderNumber { get; set; }
        [DataMember(Order = 1)] public string TestCode { get; set; }
        [DataMember(Order = 2)] public string TestName { get; set; }
        [DataMember(Order = 3)] public string Value { get; set; }
        [DataMember(Order = 4)] public string Units { get; set; }
        [DataMember(Order = 5)] public string ReferenceRange { get; set; }
        [DataMember(Order = 6)] public string AbnormalFlag { get; set; }
        [DataMember(Order = 7)] public DateTime ObservedOn { get; set; }
    }

    [DataContract(Namespace = Namespaces.Records)]
    public class ReferralSubmission
    {
        [DataMember(Order = 0, IsRequired = true)] public string Mrn { get; set; }
        [DataMember(Order = 1, IsRequired = true)] public string ReferringProviderName { get; set; }
        [DataMember(Order = 2)] public string ReferringProviderNpi { get; set; }
        [DataMember(Order = 3)] public string ReferringPractice { get; set; }
        [DataMember(Order = 4)] public string ReferringPhone { get; set; }
        [DataMember(Order = 5, IsRequired = true)] public string SpecialtyCode { get; set; }
        [DataMember(Order = 6)] public int Priority { get; set; }
        [DataMember(Order = 7, IsRequired = true)] public string Reason { get; set; }
        [DataMember(Order = 8)] public string DiagnosisCode { get; set; }
        [DataMember(Order = 9)] public string AttachmentFileName { get; set; }
        [DataMember(Order = 10)] public byte[] Attachment { get; set; }
    }

    [DataContract(Namespace = Namespaces.Records)]
    public class ReferralReceipt
    {
        [DataMember(Order = 0)] public string ReferralNumber { get; set; }
        [DataMember(Order = 1)] public DateTime DueBy { get; set; }
    }

    [DataContract(Namespace = Namespaces.Records)]
    public class ReferralStatusInfo
    {
        [DataMember(Order = 0)] public string ReferralNumber { get; set; }
        [DataMember(Order = 1)] public string Status { get; set; }
        [DataMember(Order = 2)] public DateTime ReceivedOn { get; set; }
        [DataMember(Order = 3)] public DateTime? DecidedOn { get; set; }
        [DataMember(Order = 4)] public string DecisionNote { get; set; }
        [DataMember(Order = 5)] public DateTime? AppointmentStart { get; set; }
    }
}
