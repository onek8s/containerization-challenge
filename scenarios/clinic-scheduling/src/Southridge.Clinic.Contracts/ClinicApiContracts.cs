using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Web;

namespace Southridge.Clinic.Contracts
{
    /// <summary>
    /// JSON API (WebHttpBinding) added in 2011 for the waiting-room displays and the phone app pilot.
    /// Callers send X-Api-Key.
    /// </summary>
    [ServiceContract(Namespace = Namespaces.Api)]
    public interface IClinicApi
    {
        [OperationContract]
        [WebGet(UriTemplate = "sites", ResponseFormat = WebMessageFormat.Json)]
        List<SiteInfo> Sites();

        [OperationContract]
        [WebGet(UriTemplate = "sites/{siteCode}/board", ResponseFormat = WebMessageFormat.Json)]
        WaitingBoard Board(string siteCode);

        [OperationContract]
        [WebGet(UriTemplate = "slots?type={appointmentTypeId}&from={from}&days={days}&site={siteId}", ResponseFormat = WebMessageFormat.Json)]
        List<SlotInfo> Slots(string appointmentTypeId, string from, string days, string siteId);

        [OperationContract]
        [WebGet(UriTemplate = "appointments/{confirmationCode}", ResponseFormat = WebMessageFormat.Json)]
        AppointmentInfo Appointment(string confirmationCode);

        [OperationContract]
        [WebInvoke(Method = "POST", UriTemplate = "appointments/{confirmationCode}/checkin", RequestFormat = WebMessageFormat.Json, ResponseFormat = WebMessageFormat.Json)]
        CheckInResult CheckIn(string confirmationCode, CheckInRequest request);

        [OperationContract]
        [WebGet(UriTemplate = "health", ResponseFormat = WebMessageFormat.Json)]
        HealthStatus Health();
    }

    [DataContract(Namespace = Namespaces.Api)]
    public class WaitingBoard
    {
        [DataMember(Name = "site", Order = 0)] public string Site { get; set; }
        [DataMember(Name = "updated", Order = 1)] public string Updated { get; set; }
        [DataMember(Name = "providers", Order = 2)] public List<BoardProvider> Providers { get; set; }
    }

    [DataContract(Namespace = Namespaces.Api)]
    public class BoardProvider
    {
        [DataMember(Name = "name", Order = 0)] public string Name { get; set; }
        [DataMember(Name = "waiting", Order = 1)] public int Waiting { get; set; }
        [DataMember(Name = "runningLateMinutes", Order = 2)] public int RunningLateMinutes { get; set; }
    }

    [DataContract(Namespace = Namespaces.Api)]
    public class CheckInRequest
    {
        [DataMember(Name = "lastName", Order = 0)] public string LastName { get; set; }
        [DataMember(Name = "dateOfBirth", Order = 1)] public string DateOfBirth { get; set; }
    }

    [DataContract(Namespace = Namespaces.Api)]
    public class CheckInResult
    {
        [DataMember(Name = "checkedIn", Order = 0)] public bool CheckedIn { get; set; }
        [DataMember(Name = "message", Order = 1)] public string Message { get; set; }
        [DataMember(Name = "copay", Order = 2)] public decimal Copay { get; set; }
        [DataMember(Name = "seeFrontDesk", Order = 3)] public bool SeeFrontDesk { get; set; }
    }

    [DataContract(Namespace = Namespaces.Api)]
    public class HealthStatus
    {
        [DataMember(Name = "status", Order = 0)] public string Status { get; set; }
        [DataMember(Name = "database", Order = 1)] public string Database { get; set; }
        [DataMember(Name = "version", Order = 2)] public string Version { get; set; }
        [DataMember(Name = "time", Order = 3)] public string Time { get; set; }
    }
}
