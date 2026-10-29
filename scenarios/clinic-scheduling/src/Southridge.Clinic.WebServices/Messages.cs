using System;

namespace Southridge.Clinic.WebServices
{
    // XML serialised message types for the ASMX services. The booking site and the kiosks hold generated
    // copies of these in their web references, so names and namespaces must not change.

    public class SiteItem
    {
        public int SiteId { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public string Address { get; set; }
        public string Phone { get; set; }
    }

    public class AppointmentTypeItem
    {
        public int AppointmentTypeId { get; set; }
        public string Name { get; set; }
        public int DurationMinutes { get; set; }
        public bool NewPatient { get; set; }
    }

    public class SlotItem
    {
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public int ProviderId { get; set; }
        public string ProviderName { get; set; }
        public int SiteId { get; set; }
        public string SiteName { get; set; }
    }

    public class PatientRegistration
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public DateTime DateOfBirth { get; set; }
        public string Sex { get; set; }
        public string Address1 { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string Zip { get; set; }
        public string MobilePhone { get; set; }
        public string Email { get; set; }
        public int? PreferredSiteId { get; set; }
        public string PortalUserName { get; set; }
    }

    public class RegistrationResponse
    {
        public bool Success { get; set; }
        public int PatientId { get; set; }
        public string Mrn { get; set; }
        public string Message { get; set; }
    }

    public class BookingResponse
    {
        public bool Success { get; set; }
        public int AppointmentId { get; set; }
        public string ConfirmationCode { get; set; }
        public string Message { get; set; }
    }

    public class AppointmentItem
    {
        public int AppointmentId { get; set; }
        public string ConfirmationCode { get; set; }
        public DateTime Start { get; set; }
        public string Status { get; set; }
        public string AppointmentType { get; set; }
        public string ProviderName { get; set; }
        public string SiteName { get; set; }
        public string SiteAddress { get; set; }
        public string SitePhone { get; set; }
        public bool CanCancel { get; set; }
    }

    public class PatientSummary
    {
        public int PatientId { get; set; }
        public string Mrn { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public DateTime DateOfBirth { get; set; }
        public string Email { get; set; }
        public string MobilePhone { get; set; }
        public decimal Balance { get; set; }
    }

    public class IntakeFormSubmission
    {
        public int PatientId { get; set; }
        public int? AppointmentId { get; set; }
        public string Medications { get; set; }
        public string Allergies { get; set; }
        public string Conditions { get; set; }
        public string ReasonForVisit { get; set; }
        public string PharmacyName { get; set; }
        public string PharmacyPhone { get; set; }
    }

    public class KioskAppointment
    {
        public int AppointmentId { get; set; }
        public DateTime Start { get; set; }
        public string FirstName { get; set; }
        public string LastInitial { get; set; }
        public string ProviderName { get; set; }
        public decimal Copay { get; set; }
        public bool HasBalance { get; set; }
        public bool IntakeFormDue { get; set; }
        public bool CanCheckIn { get; set; }
    }

    public class KioskCheckInResponse
    {
        public bool CheckedIn { get; set; }
        public bool SeeFrontDesk { get; set; }
        public string Message { get; set; }
    }

    public class KioskStatus
    {
        public string SiteName { get; set; }
        public DateTime ServerTime { get; set; }
        public int CheckedInToday { get; set; }
        public string Notice { get; set; }
    }
}
