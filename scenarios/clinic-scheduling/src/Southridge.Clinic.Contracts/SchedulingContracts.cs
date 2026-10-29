using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.ServiceModel;

namespace Southridge.Clinic.Contracts
{
    /// <summary>
    /// Scheduling service (basicHttpBinding, SOAP 1.1). Used by the patient site booking wizard and by the
    /// hospital's referral desk. Version 1 contract from 2009; members are added with higher Order only.
    /// </summary>
    [ServiceContract(Namespace = Namespaces.Scheduling, Name = "SchedulingService")]
    public interface ISchedulingService
    {
        [OperationContract]
        [FaultContract(typeof(ClinicFault))]
        List<SiteInfo> GetSites();

        [OperationContract]
        [FaultContract(typeof(ClinicFault))]
        List<AppointmentTypeInfo> GetAppointmentTypes(bool onlineOnly);

        [OperationContract]
        [FaultContract(typeof(ClinicFault))]
        List<ProviderInfo> GetProviders(int? siteId, string specialtyCode);

        [OperationContract]
        [FaultContract(typeof(ClinicFault))]
        List<SlotInfo> FindSlots(SlotSearch search);

        [OperationContract]
        [FaultContract(typeof(ClinicFault))]
        BookingConfirmation Book(BookAppointmentRequest request);

        [OperationContract]
        [FaultContract(typeof(ClinicFault))]
        void Cancel(string confirmationCode, string mrn, string reason);

        [OperationContract]
        [FaultContract(typeof(ClinicFault))]
        List<AppointmentInfo> GetAppointments(string mrn, DateTime from, DateTime to);
    }

    [DataContract(Namespace = Namespaces.Scheduling)]
    public class SiteInfo
    {
        [DataMember(Order = 0)] public int SiteId { get; set; }
        [DataMember(Order = 1)] public string Code { get; set; }
        [DataMember(Order = 2)] public string Name { get; set; }
        [DataMember(Order = 3)] public string Address { get; set; }
        [DataMember(Order = 4)] public string City { get; set; }
        [DataMember(Order = 5)] public string Phone { get; set; }
        [DataMember(Order = 6)] public bool OnlineBooking { get; set; }
    }

    [DataContract(Namespace = Namespaces.Scheduling)]
    public class AppointmentTypeInfo
    {
        [DataMember(Order = 0)] public int AppointmentTypeId { get; set; }
        [DataMember(Order = 1)] public string Code { get; set; }
        [DataMember(Order = 2)] public string Name { get; set; }
        [DataMember(Order = 3)] public int DurationMinutes { get; set; }
        [DataMember(Order = 4)] public string SpecialtyCode { get; set; }
        [DataMember(Order = 5)] public bool RequiresReferral { get; set; }
    }

    [DataContract(Namespace = Namespaces.Scheduling)]
    public class ProviderInfo
    {
        [DataMember(Order = 0)] public int ProviderId { get; set; }
        [DataMember(Order = 1)] public string Name { get; set; }
        [DataMember(Order = 2)] public string Credentials { get; set; }
        [DataMember(Order = 3)] public string SpecialtyCode { get; set; }
        [DataMember(Order = 4)] public string Npi { get; set; }
        [DataMember(Order = 5)] public bool AcceptingNewPatients { get; set; }
    }

    [DataContract(Namespace = Namespaces.Scheduling)]
    public class SlotSearch
    {
        [DataMember(Order = 0)] public int AppointmentTypeId { get; set; }
        [DataMember(Order = 1)] public DateTime FromDate { get; set; }
        [DataMember(Order = 2)] public int Days { get; set; }
        [DataMember(Order = 3)] public int? SiteId { get; set; }
        [DataMember(Order = 4)] public int? ProviderId { get; set; }
        [DataMember(Order = 5)] public int MaxResults { get; set; }
    }

    [DataContract(Namespace = Namespaces.Scheduling)]
    public class SlotInfo
    {
        [DataMember(Order = 0)] public DateTime Start { get; set; }
        [DataMember(Order = 1)] public DateTime End { get; set; }
        [DataMember(Order = 2)] public int ProviderId { get; set; }
        [DataMember(Order = 3)] public string ProviderName { get; set; }
        [DataMember(Order = 4)] public int SiteId { get; set; }
        [DataMember(Order = 5)] public string SiteName { get; set; }
    }

    [DataContract(Namespace = Namespaces.Scheduling)]
    public class BookAppointmentRequest
    {
        [DataMember(Order = 0, IsRequired = true)] public string Mrn { get; set; }
        [DataMember(Order = 1, IsRequired = true)] public int AppointmentTypeId { get; set; }
        [DataMember(Order = 2, IsRequired = true)] public int ProviderId { get; set; }
        [DataMember(Order = 3, IsRequired = true)] public int SiteId { get; set; }
        [DataMember(Order = 4, IsRequired = true)] public DateTime Start { get; set; }
        [DataMember(Order = 5)] public string Reason { get; set; }
        [DataMember(Order = 6)] public string ReferralNumber { get; set; }
        [DataMember(Order = 7)] public string BookedBy { get; set; }
    }

    [DataContract(Namespace = Namespaces.Scheduling)]
    public class BookingConfirmation
    {
        [DataMember(Order = 0)] public int AppointmentId { get; set; }
        [DataMember(Order = 1)] public string ConfirmationCode { get; set; }
        [DataMember(Order = 2)] public DateTime Start { get; set; }
        [DataMember(Order = 3)] public string ProviderName { get; set; }
        [DataMember(Order = 4)] public string SiteName { get; set; }
    }

    [DataContract(Namespace = Namespaces.Scheduling)]
    public class AppointmentInfo
    {
        [DataMember(Order = 0)] public int AppointmentId { get; set; }
        [DataMember(Order = 1)] public string ConfirmationCode { get; set; }
        [DataMember(Order = 2)] public DateTime Start { get; set; }
        [DataMember(Order = 3)] public DateTime End { get; set; }
        [DataMember(Order = 4)] public string Status { get; set; }
        [DataMember(Order = 5)] public string AppointmentType { get; set; }
        [DataMember(Order = 6)] public string ProviderName { get; set; }
        [DataMember(Order = 7)] public string SiteName { get; set; }
        [DataMember(Order = 8)] public bool CanCancelOnline { get; set; }
    }
}
