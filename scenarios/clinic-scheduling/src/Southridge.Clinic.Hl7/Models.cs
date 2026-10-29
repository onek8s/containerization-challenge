using System;
using System.Collections.Generic;

namespace Southridge.Clinic.Hl7
{
    /// <summary>Patient demographics as exchanged over HL7 (PID).</summary>
    public class Hl7Patient
    {
        public string Mrn { get; set; }
        public string ExternalId { get; set; }
        public string ExternalAuthority { get; set; }
        public string LastName { get; set; }
        public string FirstName { get; set; }
        public string MiddleName { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string Sex { get; set; }
        public string Address1 { get; set; }
        public string Address2 { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string Zip { get; set; }
        public string HomePhone { get; set; }
        public string MobilePhone { get; set; }
        public string Email { get; set; }
    }

    public class Hl7Provider
    {
        public string Npi { get; set; }
        public string LastName { get; set; }
        public string FirstName { get; set; }
        public string Credentials { get; set; }
    }

    public class Hl7Appointment
    {
        public long AppointmentId { get; set; }
        public DateTime Start { get; set; }
        public int DurationMinutes { get; set; }
        public string AppointmentTypeCode { get; set; }
        public string AppointmentTypeName { get; set; }
        public string Reason { get; set; }
        public string StatusCode { get; set; }
        public string SiteCode { get; set; }
        public string SiteName { get; set; }
        public string ResourceCode { get; set; }
        public string FillerStatus { get; set; }
        public string CancelReason { get; set; }
    }

    public class Hl7Charge
    {
        public string TransactionId { get; set; }
        public DateTime DateOfService { get; set; }
        public string ProcedureCode { get; set; }
        public string ProcedureDescription { get; set; }
        public string Modifier { get; set; }
        public int Units { get; set; }
        public decimal Amount { get; set; }
        public List<string> DiagnosisCodes { get; set; }
    }

    public class Hl7Order
    {
        public string PlacerOrderNumber { get; set; }
        public string TestCode { get; set; }
        public string TestName { get; set; }
        public DateTime OrderedAt { get; set; }
        public string Priority { get; set; }
        public string ClinicalInfo { get; set; }
    }

    public class Hl7Observation
    {
        public string Code { get; set; }
        public string Name { get; set; }
        public string ValueType { get; set; }
        public string Value { get; set; }
        public string Units { get; set; }
        public string ReferenceRange { get; set; }
        public string AbnormalFlag { get; set; }
        public string ResultStatus { get; set; }
        public DateTime? ObservedAt { get; set; }
    }

    public class Hl7Result
    {
        public Hl7Result()
        {
            Observations = new List<Hl7Observation>();
        }

        public Hl7Patient Patient { get; set; }
        public string PlacerOrderNumber { get; set; }
        public string FillerOrderNumber { get; set; }
        public string TestCode { get; set; }
        public string TestName { get; set; }
        public string OrderingProviderNpi { get; set; }
        public DateTime? ObservedAt { get; set; }
        public string ResultStatus { get; set; }
        public List<Hl7Observation> Observations { get; private set; }
    }

    public class Hl7Visit
    {
        public string EventType { get; set; }
        public string PatientClass { get; set; }
        public string VisitNumber { get; set; }
        public string Facility { get; set; }
        public string AttendingNpi { get; set; }
        public string AttendingName { get; set; }
        public DateTime? AdmitAt { get; set; }
        public DateTime? DischargeAt { get; set; }
        public string DischargeDisposition { get; set; }
        public string Diagnosis { get; set; }
    }
}
