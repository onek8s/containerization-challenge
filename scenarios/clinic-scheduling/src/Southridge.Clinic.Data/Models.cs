using System;
using System.Collections.Generic;

namespace Southridge.Clinic.Data
{
    [Serializable]
    public class PatientSearchCriteria
    {
        public string LastName { get; set; }
        public string FirstName { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string Mrn { get; set; }
        public string Phone { get; set; }

        public bool IsEmpty
        {
            get { return string.IsNullOrEmpty(LastName) && string.IsNullOrEmpty(FirstName) && !DateOfBirth.HasValue && string.IsNullOrEmpty(Mrn) && string.IsNullOrEmpty(Phone); }
        }
    }

    [Serializable]
    public class PatientSearchRow
    {
        public int PatientId { get; set; }
        public string Mrn { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public DateTime DateOfBirth { get; set; }
        public byte Sex { get; set; }
        public string HomePhone { get; set; }
        public string MobilePhone { get; set; }
        public string City { get; set; }
        public decimal Balance { get; set; }
        public string PreferredSite { get; set; }
        public DateTime? LastVisit { get; set; }
        public DateTime? NextAppointment { get; set; }

        public string DisplayName
        {
            get { return LastName + ", " + FirstName; }
        }
    }

    public class NewPatient
    {
        public string FirstName { get; set; }
        public string MiddleName { get; set; }
        public string LastName { get; set; }
        public DateTime DateOfBirth { get; set; }
        public byte Sex { get; set; }
        public string Address1 { get; set; }
        public string Address2 { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string Zip { get; set; }
        public string HomePhone { get; set; }
        public string MobilePhone { get; set; }
        public string Email { get; set; }
        public int? PreferredSiteId { get; set; }
        public string HospitalId { get; set; }
        public string PortalUserName { get; set; }
    }

    [Serializable]
    public class DayAppointment
    {
        public int AppointmentId { get; set; }
        public DateTime StartsAt { get; set; }
        public DateTime EndsAt { get; set; }
        public byte Status { get; set; }
        public byte Source { get; set; }
        public string Reason { get; set; }
        public bool IsOverbook { get; set; }
        public string ConfirmationCode { get; set; }
        public DateTime? CheckedInOn { get; set; }
        public int PatientId { get; set; }
        public string Mrn { get; set; }
        public string PatientName { get; set; }
        public DateTime DateOfBirth { get; set; }
        public string MobilePhone { get; set; }
        public decimal Balance { get; set; }
        public int ProviderId { get; set; }
        public string ProviderName { get; set; }
        public string AppointmentTypeCode { get; set; }
        public string AppointmentTypeName { get; set; }
        public string Color { get; set; }
        public string PrimaryPayer { get; set; }
        public decimal? Copay { get; set; }
        public bool HasAlert { get; set; }

        public int WaitMinutes
        {
            get { return CheckedInOn.HasValue && Status == 2 ? (int)(DateTime.Now - CheckedInOn.Value).TotalMinutes : 0; }
        }
    }

    [Serializable]
    public class CheckInMatch
    {
        public int AppointmentId { get; set; }
        public DateTime StartsAt { get; set; }
        public byte Status { get; set; }
        public string ConfirmationCode { get; set; }
        public int PatientId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public decimal Balance { get; set; }
        public string ProviderName { get; set; }
        public decimal Copay { get; set; }
        public bool IntakeFormDue { get; set; }
    }

    public class ReminderCandidate
    {
        public int AppointmentId { get; set; }
        public DateTime StartsAt { get; set; }
        public string ConfirmationCode { get; set; }
        public int PatientId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string MobilePhone { get; set; }
        public string PreferredContact { get; set; }
        public string ProviderName { get; set; }
        public string SiteName { get; set; }
        public string SiteAddress { get; set; }
        public string SitePhone { get; set; }
        public string AppointmentTypeName { get; set; }
    }

    public class NoShow
    {
        public int AppointmentId { get; set; }
        public int PatientId { get; set; }
        public int ProviderId { get; set; }
        public int SiteId { get; set; }
        public DateTime StartsAt { get; set; }
    }

    public class WaitlistCandidate
    {
        public int WaitlistEntryId { get; set; }
        public int PatientId { get; set; }
        public string PatientName { get; set; }
        public string MobilePhone { get; set; }
        public string Email { get; set; }
        public int AppointmentTypeId { get; set; }
        public byte Priority { get; set; }
        public DateTime EarliestDate { get; set; }
        public DateTime? LatestDate { get; set; }
        public DateTime CreatedOn { get; set; }
    }

    public class OutboundMessage
    {
        public int MessageId { get; set; }
        public int Attempts { get; set; }
        public string MessageType { get; set; }
        public string ControlId { get; set; }
        public string Payload { get; set; }
    }

    [Serializable]
    public class MessageLogRow
    {
        public int MessageId { get; set; }
        public byte Direction { get; set; }
        public string Partner { get; set; }
        public string MessageType { get; set; }
        public string ControlId { get; set; }
        public byte Status { get; set; }
        public int Attempts { get; set; }
        public string Error { get; set; }
        public DateTime CreatedOn { get; set; }
        public DateTime? ProcessedOn { get; set; }
        public int? PatientId { get; set; }
        public string Mrn { get; set; }
        public string LastName { get; set; }

        public string DirectionText
        {
            get { return Direction == 0 ? "In" : "Out"; }
        }

        public string StatusText
        {
            get { return ((Southridge.Clinic.Domain.MessageStatus)Status).ToString(); }
        }
    }

    public class PatientUpsertResult
    {
        public int PatientId { get; set; }
        public string Action { get; set; }
    }

    public class ReadyEncounter
    {
        public int EncounterId { get; set; }
        public DateTime DateOfService { get; set; }
        public int PatientId { get; set; }
        public string Mrn { get; set; }
        public string PatientName { get; set; }
        public int? PatientInsuranceId { get; set; }
        public int? PayerId { get; set; }
        public string PayerCode { get; set; }
        public int? PayerType { get; set; }
        public decimal TotalCharge { get; set; }
        public int ChargeCount { get; set; }
    }

    /// <summary>Everything the 837P writer needs for one claim (from usp_Claim_BuildData).</summary>
    public class ClaimData
    {
        public ClaimData()
        {
            Diagnoses = new List<ClaimDiagnosis>();
            Lines = new List<ClaimLine>();
        }

        public int ClaimId { get; set; }
        public string ClaimNumber { get; set; }
        public DateTime DateOfService { get; set; }
        public decimal TotalCharge { get; set; }
        public string FrequencyCode { get; set; }
        public string OriginalClaimNumber { get; set; }
        public string PayerClaimNumber { get; set; }
        public string Mrn { get; set; }
        public string PatientFirstName { get; set; }
        public string PatientMiddleName { get; set; }
        public string PatientLastName { get; set; }
        public DateTime PatientDateOfBirth { get; set; }
        public byte PatientSex { get; set; }
        public string PatientAddress1 { get; set; }
        public string PatientAddress2 { get; set; }
        public string PatientCity { get; set; }
        public string PatientState { get; set; }
        public string PatientZip { get; set; }
        public string MemberId { get; set; }
        public string GroupNumber { get; set; }
        public byte Relationship { get; set; }
        public string SubscriberFirstName { get; set; }
        public string SubscriberLastName { get; set; }
        public DateTime? SubscriberDateOfBirth { get; set; }
        public string PayerName { get; set; }
        public string ClaimPayerId { get; set; }
        public string FilingIndicator { get; set; }
        public int PayerType { get; set; }
        public string PayerAddress1 { get; set; }
        public string PayerCity { get; set; }
        public string PayerState { get; set; }
        public string PayerZip { get; set; }
        public string RenderingNpi { get; set; }
        public string RenderingFirstName { get; set; }
        public string RenderingLastName { get; set; }
        public string TaxonomyCode { get; set; }
        public string SiteName { get; set; }
        public string SiteAddress1 { get; set; }
        public string SiteCity { get; set; }
        public string SiteState { get; set; }
        public string SiteZip { get; set; }
        public string PlaceOfService { get; set; }
        public string TaxId { get; set; }
        public string GroupNpi { get; set; }
        public string AuthorizationNumber { get; set; }
        public List<ClaimDiagnosis> Diagnoses { get; private set; }
        public List<ClaimLine> Lines { get; private set; }
    }

    public class ClaimDiagnosis
    {
        public int Sequence { get; set; }
        public string Code { get; set; }
        public int CodeSet { get; set; }
    }

    public class ClaimLine
    {
        public int ChargeId { get; set; }
        public int LineNumber { get; set; }
        public string CptCode { get; set; }
        public string Modifier1 { get; set; }
        public string Modifier2 { get; set; }
        public int Units { get; set; }
        public decimal Amount { get; set; }
        public string DiagnosisPointers { get; set; }
    }

    public class StatementCandidate
    {
        public int PatientId { get; set; }
        public string Mrn { get; set; }
        public string PatientName { get; set; }
        public decimal Balance { get; set; }
    }

    public class StatementData
    {
        public StatementData()
        {
            Lines = new List<StatementLine>();
            Payments = new List<StatementPayment>();
        }

        public int PatientId { get; set; }
        public string Mrn { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Address1 { get; set; }
        public string Address2 { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string Zip { get; set; }
        public decimal Balance { get; set; }
        public decimal PreviousAmountDue { get; set; }
        public int PreviousDunningLevel { get; set; }
        public List<StatementLine> Lines { get; private set; }
        public List<StatementPayment> Payments { get; private set; }
    }

    public class StatementLine
    {
        public DateTime DateOfService { get; set; }
        public string CptCode { get; set; }
        public string Description { get; set; }
        public int Units { get; set; }
        public decimal Amount { get; set; }
        public decimal InsurancePaid { get; set; }
        public decimal Adjustment { get; set; }
        public decimal PatientAmount { get; set; }
        public string ProviderName { get; set; }
        public string SiteName { get; set; }
    }

    public class StatementPayment
    {
        public DateTime PostedOn { get; set; }
        public string Source { get; set; }
        public string Method { get; set; }
        public decimal Amount { get; set; }
        public string Reference { get; set; }
    }

    public class ApiClientInfo
    {
        public int ApiClientId { get; set; }
        public string Name { get; set; }
        public string ClientType { get; set; }
        public int? SiteId { get; set; }
    }

    public class AppSettingRow
    {
        public string Name { get; set; }
        public string Value { get; set; }
        public string Description { get; set; }
        public string ModifiedBy { get; set; }
        public DateTime? ModifiedOn { get; set; }
    }

    public class UnbilledEncounter
    {
        public int EncounterId { get; set; }
        public DateTime DateOfService { get; set; }
        public string Mrn { get; set; }
        public string PatientName { get; set; }
        public string ProviderName { get; set; }
        public string SiteCode { get; set; }
    }

    [Serializable]
    public class PatientClaimRow
    {
        public int ClaimId { get; set; }
        public string ClaimNumber { get; set; }
        public DateTime DateOfService { get; set; }
        public string PayerName { get; set; }
        public byte Status { get; set; }
        public decimal TotalCharge { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal AdjustmentAmount { get; set; }
        public decimal PatientResponsibility { get; set; }
        public string RejectReason { get; set; }
        public DateTime? SubmittedOn { get; set; }

        public string StatusText
        {
            get { return ((Southridge.Clinic.Domain.ClaimStatus)Status).ToString(); }
        }
    }

    [Serializable]
    public class StatementRow
    {
        public int StatementId { get; set; }
        public DateTime StatementDate { get; set; }
        public decimal AmountDue { get; set; }
        public DateTime DueDate { get; set; }
        public int DunningLevel { get; set; }
        public string FileName { get; set; }
    }
}
